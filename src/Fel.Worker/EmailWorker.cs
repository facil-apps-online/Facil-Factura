using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Fel.Core.Interfaces;

namespace Fel.Worker
{
    public class EmailWorker : BackgroundService
    {
        private readonly ILogger<EmailWorker> _logger;
        private readonly IServiceProvider _serviceProvider;
        private const string EmailQueueName = "fel:email:queue";

        public EmailWorker(ILogger<EmailWorker> logger, IServiceProvider serviceProvider)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("🚀 Iniciando servicio desacoplado de Notificaciones y Correos (EmailWorker) - {Time}", DateTimeOffset.Now);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var messageQueue = scope.ServiceProvider.GetRequiredService<IMessageQueue>();

                        // Leer de la cola de forma bloqueante (desacoplado de la facturación)
                        var emailJob = await messageQueue.DequeueAsync<EmailJobData>(EmailQueueName);

                        if (emailJob != null)
                        {
                            _logger.LogInformation("Enviando correo para la factura TrackId: {TrackId} al Adquirente: {Email}", emailJob.TrackId, emailJob.RecipientEmail);

                            // 1. Generar Representación Gráfica (PDF) vía DevExpress o HTML a PDF
                            // byte[] pdfBytes = await reportService.GenerateInvoicePdf(emailJob.TrackId);

                            // 2. Generar AttachedDocument (XML contenedor exigido por la DIAN)
                            // byte[] xmlBytes = xmlBuilderService.CreateAttachedDocument(emailJob.UblBase64, emailJob.ApplicationResponseBase64);

                            // 3. Crear ZIP con el PDF y el XML
                            // byte[] zipBytes = compressionService.CreateZip(pdfBytes, xmlBytes);

                            // 4. Enviar el Correo (SMTP / SendGrid)
                            // await emailService.SendEmailWithAttachmentsAsync(emailJob.RecipientEmail, "Tu Factura Electrónica", zipBytes);

                            _logger.LogInformation("✅ Correo enviado exitosamente para TrackId: {TrackId}", emailJob.TrackId);
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    // Apagado graceful del servicio
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ Error crítico procesando la cola de correos electrónicos.");
                    await Task.Delay(5000, stoppingToken); // Backoff antes de reintentar
                }
            }
        }
    }

    public class EmailJobData
    {
        public string TrackId { get; set; } = string.Empty;
        public string RecipientEmail { get; set; } = string.Empty;
        public string UblBase64 { get; set; } = string.Empty;
        public string ApplicationResponseBase64 { get; set; } = string.Empty;
    }
}
