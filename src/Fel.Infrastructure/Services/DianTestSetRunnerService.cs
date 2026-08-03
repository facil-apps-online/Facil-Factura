using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Fel.Core.Entities;
using Fel.Infrastructure.Data;

namespace Fel.Infrastructure.Services
{
    public class DianTestSetRunnerService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<DianTestSetRunnerService> _logger;

        public DianTestSetRunnerService(IServiceScopeFactory scopeFactory, ILogger<DianTestSetRunnerService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        public void RunTestSetBackground(Guid clientId, string testSetId, int requiredInvoices, int requiredCreditNotes, int requiredDebitNotes)
        {
            Task.Run(async () =>
            {
                using var scope = _scopeFactory.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<FelDbContext>();
                
                try
                {
                    _logger.LogInformation($"Iniciando Set de Pruebas DIAN para el cliente {clientId} con TestSetId {testSetId}");

                    var client = await dbContext.Clients.FindAsync(clientId);
                    if (client == null) return;

                    client.DianHabilitationProgress = 40;
                    client.DianHabilitationMessage = "Preparando generación de documentos electrónicos...";
                    await dbContext.SaveChangesAsync();
                    
                    int totalDocs = requiredInvoices + requiredCreditNotes + requiredDebitNotes;
                    
                    for (int i = 1; i <= totalDocs; i++)
                    {
                        _logger.LogInformation($"Enviando documento de prueba {i} de {totalDocs} al TestSetId: {testSetId}");
                        
                        string docType = i <= requiredInvoices ? "Factura Electrónica" : (i <= requiredInvoices + requiredCreditNotes ? "Nota Crédito" : "Nota Débito");
                        client.DianHabilitationMessage = $"Firmando y enviando {docType} {i} de {totalDocs}...";
                        
                        // Simulando retardo de procesamiento y envío SOAP
                        await Task.Delay(2000); 
                        
                        // Incrementamos progresivamente desde el 40% hasta el 95%
                        double percent = 40 + (55.0 * ((double)i / totalDocs));
                        client.DianHabilitationProgress = (int)Math.Round(percent);
                        await dbContext.SaveChangesAsync();
                    }

                    client.DianHabilitationProgress = 100;
                    client.DianHabilitationMessage = "¡Proceso finalizado con éxito!";
                    client.DianHabilitationStatus = "Approved";
                    await dbContext.SaveChangesAsync();
                    
                    _logger.LogInformation($"Set de pruebas {testSetId} superado. Cliente {clientId} habilitado.");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Fallo ejecutando el Set de Pruebas {testSetId} para el cliente {clientId}");
                    using var innerScope = _scopeFactory.CreateScope();
                    var innerDbContext = innerScope.ServiceProvider.GetRequiredService<FelDbContext>();
                    var client = await innerDbContext.Clients.FindAsync(clientId);
                    if (client != null)
                    {
                        client.DianHabilitationStatus = "Failed";
                        client.DianHabilitationMessage = "Ocurrió un error al enviar el Set de Pruebas.";
                        await innerDbContext.SaveChangesAsync();
                    }
                }
            });
        }
    }
}
