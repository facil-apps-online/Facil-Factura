using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Fel.Core.Interfaces;
using Fel.Core.Models;
using Fel.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Fel.Worker
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly IMessageQueue _messageQueue;
        private const string QueueName = "fel:invoices:queue";

        public Worker(
            ILogger<Worker> logger,
            IServiceProvider serviceProvider,
            IMessageQueue messageQueue)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
            _messageQueue = messageQueue;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Worker iniciado. Escuchando en la cola: {QueueName}", QueueName);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // Desencolar
                    var invoiceData = await _messageQueue.DequeueAsync<UblInvoiceData>(QueueName);

                    if (invoiceData == null)
                    {
                        await Task.Delay(1000, stoppingToken); // Fallback delay
                        continue;
                    }

                    _logger.LogInformation("Factura {DocumentNumber} recibida de la cola. Procesando...", invoiceData.DocumentNumber);

                    // Usar un Scope para servicios Scoped
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var ublGenerator = scope.ServiceProvider.GetRequiredService<IUblGenerator>();
                        var xmlSigner = scope.ServiceProvider.GetRequiredService<IXmlSigner>();
                        var cryptoVault = scope.ServiceProvider.GetRequiredService<ICryptoVault>();
                        var dianClient = scope.ServiceProvider.GetRequiredService<IDianSoapClient>();

                        var dbContext = scope.ServiceProvider.GetRequiredService<FelDbContext>();

                        // 1. Build XML UBL 2.1
                        string xmlBase = ublGenerator.GenerateInvoiceXml(invoiceData);

                        // 2. Extraer Certificado .p12 de la Bóveda conectando a BD
                        string issuerTaxId = invoiceData.Issuer.TaxId;
                        var certificateInfo = await dbContext.Certificates
                            .Include(c => c.Client)
                            .FirstOrDefaultAsync(c => c.Client.TaxId == issuerTaxId && c.IsActive);
                            
                        if (certificateInfo == null)
                        {
                            _logger.LogError("No hay certificado activo para el cliente con TaxId: {TaxId}", issuerTaxId);
                            continue; // No podemos firmar, skip a la siguiente
                        }
                        
                        string mockPath = certificateInfo.FileName; // Archivo real guardado
                        string mockPasswordEncrypted = certificateInfo.EncryptedPassword; // Contraseña en BD
                        
                        string signedXml = xmlBase; // Default
                        
                        try 
                        {
                            var cert = cryptoVault.GetCertificate(mockPath, mockPasswordEncrypted);
                            signedXml = xmlSigner.SignXml(xmlBase, cert);
                        }
                        catch(Exception ex)
                        {
                            _logger.LogWarning("Modo Simulación: No se pudo firmar el XML porque no existe el .p12 de pruebas. Error: {Message}", ex.Message);
                        }

                        // 3. Convertir a Base64 para WCF
                        string base64Xml = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(signedXml));

                        // 4. Send to DIAN
                        _logger.LogInformation("Enviando XML firmado a la DIAN (WS-Security)...");
                        
                        try 
                        {
                            var cert = cryptoVault.GetCertificate(mockPath, mockPasswordEncrypted);
                            string dianResponse = await dianClient.SendBillAsync($"FE{invoiceData.DocumentNumber}.xml", base64Xml, cert);
                            _logger.LogInformation("Respuesta DIAN: {Response}", dianResponse);
                        }
                        catch(Exception ex)
                        {
                            _logger.LogWarning("Modo Simulación: El endpoint de la DIAN rechazó el envío (o falta el certificado válido). Error: {Message}", ex.Message);
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    // Normal cancellation
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error inesperado procesando la cola de facturas.");
                    await Task.Delay(5000, stoppingToken); // Backoff on error
                }
            }
        }
    }
}
