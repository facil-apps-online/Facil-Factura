using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Fel.Core.Entities;
using Fel.Core.Interfaces;
using Fel.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Fel.Worker
{
    // Sincroniza el estado de las solicitudes de certificado con Viafirma (Fase 3 de
    // docs/certificados-electronicos-viafirma-especificacion.md) y, cuando detecta que el .p7b ya
    // está listo, dispara el ensamble del .p12 e informa al cliente por correo (si su tenant no lo apagó). Si Viafirma rechaza
    // la solicitud o falla, devuelve al saldo prepagado del tenant lo reservado (ver CertificateBilling). No cubre todavía
    // renovación automática.
    public class CertificatePollingWorker : BackgroundService
    {
        private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(15);

        private static readonly CertificateRequestStatus[] PendingStatuses =
        {
            CertificateRequestStatus.Submitted,
            CertificateRequestStatus.WaitingForIdentity,
            CertificateRequestStatus.IdentityReview,
            CertificateRequestStatus.DocumentsRequired,
            CertificateRequestStatus.DocumentsSubmitted,
            CertificateRequestStatus.ProviderReview,
            CertificateRequestStatus.CertificateProcessing,
            CertificateRequestStatus.ReadyToDownload
        };

        private readonly ILogger<CertificatePollingWorker> _logger;
        private readonly IServiceProvider _serviceProvider;

        public CertificatePollingWorker(ILogger<CertificatePollingWorker> logger, IServiceProvider serviceProvider)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Iniciando CertificatePollingWorker (sincronización cada {Minutes} minutos)", PollInterval.TotalMinutes);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessPendingRequestsAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error en el ciclo de CertificatePollingWorker");
                }

                try
                {
                    await Task.Delay(PollInterval, stoppingToken);
                }
                catch (TaskCanceledException)
                {
                }
            }
        }

        private async Task ProcessPendingRequestsAsync(CancellationToken stoppingToken)
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<FelDbContext>();
            var provider = scope.ServiceProvider.GetRequiredService<ICertificateProvider>();
            var contextFactory = scope.ServiceProvider.GetRequiredService<ICertificateProviderContextFactory>();
            var assemblyService = scope.ServiceProvider.GetRequiredService<ICertificateAssemblyService>();
            var emailOutbox = scope.ServiceProvider.GetRequiredService<IEmailOutbox>();

            // Borradores huérfanos: la solicitud se guarda (y su precio se reserva) justo antes de enviarla a Viafirma; si el envío nunca
            // terminó (p. ej. se reinició la API en ese instante), queda fallida y se devuelve lo reservado.
            var orphanCutoff = DateTime.UtcNow.AddMinutes(-30);
            var orphans = await dbContext.CertificateRequests
                .Where(r => r.Status == CertificateRequestStatus.Draft && r.CreatedAt < orphanCutoff)
                .ToListAsync(stoppingToken);
            foreach (var orphan in orphans)
            {
                orphan.Status = CertificateRequestStatus.Failed;
                orphan.LastErrorCode = "certificate_submission_interrupted";
                orphan.LastErrorMessage = "El envío a Viafirma no terminó. Vuelve a solicitarlo; no se descontó saldo.";
                orphan.UpdatedAt = DateTime.UtcNow;
                await Fel.Infrastructure.Certificates.CertificateBilling.ReleaseAsync(dbContext, orphan.Id, "el envío a Viafirma no terminó", "system", stoppingToken);
            }
            if (orphans.Count > 0) await dbContext.SaveChangesAsync(stoppingToken);

            var requests = await dbContext.CertificateRequests
                .Include(r => r.Client).ThenInclude(c => c.Tenant)
                .Include(r => r.Profile).ThenInclude(p => p.Provider)
                .Where(r => PendingStatuses.Contains(r.Status))
                .ToListAsync(stoppingToken);

            if (requests.Count == 0) return;

            _logger.LogInformation("Revisando {Count} solicitudes de certificado con Viafirma", requests.Count);

            foreach (var request in requests)
            {
                try
                {
                    await SyncOneRequestAsync(request, dbContext, provider, contextFactory, stoppingToken);

                    if (request.Status == CertificateRequestStatus.ReadyToDownload)
                    {
                        var assembled = await assemblyService.AssembleAndInstallAsync(request, stoppingToken);
                        // Un tenant que revende puede entregar él mismo el certificado (lo descarga desde su portal).
                        if (request.Client.Tenant.SendCertificateToHolderByEmail)
                            await QueueCertificateEmailsAsync(request.Client, request.Id, request.InstallOnPlatform, assembled, emailOutbox, dbContext, stoppingToken);
                    }
                }
                catch (Exception ex)
                {
                    request.RetryCount += 1;
                    request.LastErrorMessage = ex.Message;
                    request.NextRetryAt = DateTime.UtcNow.Add(PollInterval);
                    _logger.LogError(ex, "No se pudo sincronizar/ensamblar CertificateRequest {RequestId}", request.Id);
                    await dbContext.SaveChangesAsync(stoppingToken);
                }
            }
        }

        private static async Task SyncOneRequestAsync(
            CertificateRequest request,
            FelDbContext dbContext,
            ICertificateProvider provider,
            ICertificateProviderContextFactory contextFactory,
            CancellationToken stoppingToken)
        {
            // Ya está lista para ensamblar — no hace falta volver a preguntarle el estado a
            // Viafirma, el siguiente paso es descargar el .p7b directamente.
            if (request.Status == CertificateRequestStatus.ReadyToDownload) return;

            var context = await contextFactory.CreateAsync(request.Profile.Provider.Key, request.Environment.ToString(), stoppingToken);
            var advanced = await provider.GetAdvancedStatusAsync(request.ProviderRequestCode, context, stoppingToken);

            var previousStatus = request.Status;
            var mappedStatus = MapExternalStatus(advanced.Status);

            request.ProviderStatus = advanced.Status;
            request.AdvancedAccreditedStatus = advanced.Accredited;
            request.AdvancedPaymentStatus = advanced.Paid;
            request.LastProviderSyncAt = DateTime.UtcNow;
            request.NextProviderSyncAt = DateTime.UtcNow.Add(PollInterval);

            if (mappedStatus.HasValue && mappedStatus.Value != previousStatus)
            {
                request.Status = mappedStatus.Value;
                dbContext.CertificateEvents.Add(new CertificateEvent
                {
                    Id = Guid.NewGuid(),
                    CertificateRequestId = request.Id,
                    EventType = CertificateEventType.ProviderStatusChanged,
                    FromStatus = previousStatus,
                    ToStatus = mappedStatus.Value,
                    ProviderStatus = advanced.Status,
                    ActorType = "system",
                    OccurredAt = DateTime.UtcNow
                });

                // Rechazada o fallida: no se emitió, se devuelve al saldo lo reservado.
                if (mappedStatus.Value is CertificateRequestStatus.Rejected or CertificateRequestStatus.Failed)
                    await Fel.Infrastructure.Certificates.CertificateBilling.ReleaseAsync(
                        dbContext, request.Id, mappedStatus.Value == CertificateRequestStatus.Rejected ? "rechazada por Viafirma" : "falló en Viafirma", "system", stoppingToken);
            }

            await dbContext.SaveChangesAsync(stoppingToken);
        }

        // Tabla de la sección 8.1 de la especificación (docs/certificados-electronicos-viafirma-especificacion.md).
        private static CertificateRequestStatus? MapExternalStatus(string externalStatus) => externalStatus switch
        {
            "accreditation" => CertificateRequestStatus.WaitingForIdentity,
            "accreditation_check" or "checking" or "collate_data" => CertificateRequestStatus.IdentityReview,
            "docRequired" => CertificateRequestStatus.DocumentsRequired,
            "docUploaded" => CertificateRequestStatus.DocumentsSubmitted,
            "proposeFor" or "proposedToAcceptance" or "All_Ok" => CertificateRequestStatus.ProviderReview,
            "inProcess" or "Cite_To_Finish" or "processingContract" => CertificateRequestStatus.CertificateProcessing,
            "Generated_Not_Downloaded" or "signedContract" => CertificateRequestStatus.ReadyToDownload,
            "Generated_And_Downloaded" => CertificateRequestStatus.Downloaded,
            "rejected" => CertificateRequestStatus.Rejected,
            "fail" => CertificateRequestStatus.Failed,
            _ => null
        };

        // El .p12 y su contraseña se mandan en dos correos separados para que, si alguno se filtra o se reenvía por error, no viajen
        // juntas las dos piezas necesarias para usar el certificado. Ambos quedan en la bandeja de salida: la contraseña queda retenida
        // y solo sale cuando el correo con el archivo quedó enviado (si ese falla, no sale).
        private static async Task QueueCertificateEmailsAsync(
            Client client, Guid certificateRequestId, bool installedOnPlatform, Fel.Core.Interfaces.AssembledCertificate assembled,
            IEmailOutbox emailOutbox, FelDbContext dbContext, CancellationToken stoppingToken)
        {
            var recipient = !string.IsNullOrWhiteSpace(client.LegalRepresentativeEmail) ? client.LegalRepresentativeEmail! : client.Email;
            if (string.IsNullOrWhiteSpace(recipient)) return;

            var zipBytes = ZipPkcs12(assembled.Pkcs12Bytes, $"{client.CompanyName}.p12");

            var fileEmail = emailOutbox.Add(new OutboundEmail
            {
                Kind = EmailKind.Certificate, Client = client, RecipientEmail = recipient, CertificateRequestId = certificateRequestId,
                Subject = installedOnPlatform ? "Tu certificado digital ya está instalado" : "Tu certificado digital fue emitido",
                BodyHtml = (installedOnPlatform ? "<p>Tu certificado digital para facturación electrónica ya quedó instalado y activo en Facil Factura.</p>" : "<p>Tu certificado digital ya fue emitido.</p>") +
                    "<p>Adjunto va el archivo <strong>.p12</strong> comprimido, por si necesitas usarlo en otro sistema.</p>" +
                    "<p><strong>La contraseña del archivo te llega en un correo aparte</strong>, por seguridad.</p>",
                Attachments = new[] { new EmailAttachment { FileName = "certificado.zip", Content = zipBytes, ContentType = "application/zip" } }
            });

            var passwordEmail = emailOutbox.Add(new OutboundEmail
            {
                Kind = EmailKind.CertificatePassword, Client = client, RecipientEmail = recipient, CertificateRequestId = certificateRequestId,
                DependsOnId = fileEmail.Id, Sensitive = true,
                Subject = "Contraseña de tu certificado digital",
                BodyHtml = "<p>Esta es la contraseña del archivo .p12 que te enviamos en el correo anterior:</p>" +
                    $"<p style=\"font-size:1.2em;font-weight:bold;letter-spacing:1px\">{System.Net.WebUtility.HtmlEncode(assembled.Password)}</p>" +
                    "<p>Guárdala en un lugar seguro — no la volveremos a enviar.</p>"
            });

            await dbContext.SaveChangesAsync(stoppingToken);
            await emailOutbox.NotifyAsync(fileEmail, passwordEmail);
        }

        private static byte[] ZipPkcs12(byte[] pkcs12Bytes, string entryName)
        {
            using var memoryStream = new MemoryStream();
            using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, leaveOpen: true))
            {
                var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
                using var entryStream = entry.Open();
                entryStream.Write(pkcs12Bytes, 0, pkcs12Bytes.Length);
            }
            return memoryStream.ToArray();
        }
    }
}
