using System;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Fel.Core.Interfaces;
using Fel.Core.Models;
using Fel.Core.Entities;
using Fel.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Fel.Infrastructure.Services;

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
                            
                        // Un Client de prueba de developer (IsDeveloperSandbox) nunca va a tener
                        // certificado. Antes caía en el "continue" de abajo y el mensaje se
                        // descartaba en silencio: el developer recibía un OK del endpoint y su
                        // factura se evaporaba sin dejar ni rastro en Documents. Ahora se le simula
                        // la aceptación, igual que hacen los RIPS (ver SandboxSimulation).
                        Fel.Core.Entities.Client? clienteSandbox = null;
                        if (certificateInfo == null)
                        {
                            clienteSandbox = await dbContext.Clients
                                .FirstOrDefaultAsync(c => c.TaxId == issuerTaxId && c.IsDeveloperSandbox);

                            if (clienteSandbox == null)
                            {
                                _logger.LogError("No hay certificado activo para el cliente con TaxId: {TaxId}", issuerTaxId);
                                continue; // No podemos firmar, skip a la siguiente
                            }

                            // El CUFE incluye la clave técnica de la resolución. Si el Client de
                            // prueba no tiene una, se rellena ANTES de armar el XML para que el
                            // XML y el CUFE que se le devuelven sigan siendo consistentes entre sí.
                            if (string.IsNullOrWhiteSpace(invoiceData.TechnicalKey))
                            {
                                invoiceData.TechnicalKey = Fel.Infrastructure.Services.SandboxSimulation.ClaveTecnicaDeRelleno;
                                xmlBase = ublGenerator.GenerateInvoiceXml(invoiceData);
                            }
                        }

                        // 2.1 REGISTRO DE TRANSACCIÓN E INMUTABILIDAD DE PLANTILLA
                        // El emisor sale del certificado cuando lo hay, y del Client de prueba
                        // cuando estamos simulando.
                        var emisor = certificateInfo?.Client ?? clienteSandbox!;

                        var docType = await dbContext.DocumentTypes
                            .FirstOrDefaultAsync(d => d.DianCode == invoiceData.DianCode && (invoiceData.OperationType == null || d.OperationType == invoiceData.OperationType));

                        var setting = docType != null
                            ? await dbContext.ClientDocumentSettings.FirstOrDefaultAsync(s => s.ClientId == emisor.Id && s.DocumentTypeId == docType.Id)
                            : null;

                        var newDoc = new Document
                        {
                            Id = Guid.NewGuid(),
                            ClientId = emisor.Id,
                            BranchId = invoiceData.BranchId ?? await BranchProvisioning.MainBranchIdAsync(dbContext, emisor.Id),
                            TrackingId = $"{invoiceData.Prefix}{invoiceData.DocumentNumber}",
                            TypeCode = docType?.Code ?? "UNKNOWN",
                            Number = invoiceData.DocumentNumber,
                            Prefix = invoiceData.Prefix,
                            Status = "PROCESSING",
                            DocumentTypeId = docType?.Id,
                            Cufe = ublGenerator.CalculateCufe(invoiceData),
                            // Tarifa vigente del cliente al momento de emitir (antes quedaba fijo en
                            // 0 y el corte mensual de Superadmin generaba cobros en $0 sin importar
                            // el volumen real).
                            PriceCharged = emisor.PricePerDocument,
                            IntegratorId = emisor.IntegratorId,
                            CreatedAt = DateTime.UtcNow,
                            IssueDate = invoiceData.IssueDate,
                            UsedTemplateId = setting?.SelectedTemplateId
                        };

                        dbContext.Documents.Add(newDoc);
                        await dbContext.SaveChangesAsync();

                        // Camino simulado: el documento queda guardado y visible en
                        // document-tracking con su CUFE real, pero no se firma ni se transmite.
                        // BillingMetricsService excluye los Clients sandbox, así que no se cobra.
                        if (clienteSandbox != null)
                        {
                            newDoc.Status = "APPROVED";
                            newDoc.DianResponseMessage = Fel.Infrastructure.Services.SandboxSimulation.Mensaje(
                                usoClaveTecnicaDeRelleno: invoiceData.TechnicalKey == Fel.Infrastructure.Services.SandboxSimulation.ClaveTecnicaDeRelleno);
                            newDoc.ProcessedAt = DateTime.UtcNow;
                            await dbContext.SaveChangesAsync();

                            _logger.LogInformation(
                                "Documento {Numero} simulado para el Client de prueba {TaxId} (sandbox de developer): no se transmitió a la DIAN.",
                                invoiceData.DocumentNumber, issuerTaxId);
                            continue;
                        }

                        // 2.2 Firmar el XML. Si el certificado no existe o la firma falla, no se envía
                        // nada a la DIAN sin firmar — se rechaza aquí mismo, porque un XML sin firma
                        // jamás sería válido de todas formas.
                        // A esta altura certificateInfo no puede ser nulo: el único caso sin
                        // certificado es el sandbox, y ese ya salió con "continue" más arriba.
                        var certificadoFirmante = certificateInfo!;

                        string signedXml;
                        X509Certificate2 cert;
                        try
                        {
                            cert = cryptoVault.GetCertificate(certificadoFirmante.FileName, certificadoFirmante.EncryptedPassword);
                            var certChain = cryptoVault.GetCertificateChain(certificadoFirmante.FileName, certificadoFirmante.EncryptedPassword);
                            signedXml = xmlSigner.SignXml(xmlBase, cert, certChain);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError("No se pudo firmar el XML (certificado inválido o faltante) para {DocumentNumber}. Error: {Message}", invoiceData.DocumentNumber, ex.Message);

                            newDoc.Status = "REJECTED";
                            newDoc.DianResponseMessage = $"No se pudo firmar el documento: {ex.Message}";
                            newDoc.ProcessedAt = DateTime.UtcNow;
                            await dbContext.SaveChangesAsync();
                            continue;
                        }

                        // 3. Nombres de archivo exigidos por el anexo técnico (numeral 6.5.7/6.5.8) —
                        // SendBillAsync exige el XML firmado dentro de un .zip, no el XML suelto en
                        // Base64 (eso lo arma DianSoapClient.SendBillAsync internamente ahora).
                        var fileSequence = await Fel.Infrastructure.Services.DianFileNaming.ClaimNextSequenceAsync(dbContext, certificadoFirmante.ClientId);
                        var zipFileName = Fel.Infrastructure.Services.DianFileNaming.BuildFileName("z", certificadoFirmante.Client.TaxId, fileSequence);
                        var entryFileName = Fel.Infrastructure.Services.DianFileNaming.BuildFileName(Fel.Infrastructure.Services.DianFileNaming.FacturaVenta, certificadoFirmante.Client.TaxId, fileSequence);

                        // 4. Send to DIAN
                        _logger.LogInformation("Enviando XML firmado a la DIAN (WS-Security), ambiente {Environment}...", invoiceData.Environment);

                        try
                        {
                            string dianResponse = await dianClient.SendBillAsync(zipFileName, entryFileName, signedXml, cert, invoiceData.Environment);
                            _logger.LogInformation("Respuesta DIAN: {Response}", dianResponse);

                            // SendBillAsync es asíncrono del lado de la DIAN: esta respuesta solo
                            // confirma que el envío llegó, no que fue validado. El resultado final
                            // (aceptado/rechazado) requiere consultarlo después con GetStatus por
                            // TrackId — eso queda pendiente de implementar, así que el documento se
                            // deja en PROCESSING en vez de asumir que ya quedó aprobado.
                            newDoc.Status = "PROCESSING";
                            newDoc.DianResponseMessage = dianResponse;
                            await dbContext.SaveChangesAsync();
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "El endpoint de la DIAN rechazó el envío de {DocumentNumber}.", invoiceData.DocumentNumber);

                            newDoc.Status = "REJECTED";
                            newDoc.DianResponseMessage = ex.Message;
                            newDoc.ProcessedAt = DateTime.UtcNow;
                            await dbContext.SaveChangesAsync();
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
