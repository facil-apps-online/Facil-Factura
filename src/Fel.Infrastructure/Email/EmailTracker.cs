using System.Text.Json;
using Fel.Core.Entities;
using Fel.Core.Interfaces;
using Fel.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Fel.Infrastructure.Email
{
    // Pregunta a SendPulse qué pasó con los correos que salieron por la plataforma: si el servidor del destinatario los aceptó o los
    // rechazó y si se abrieron. Lo corre el worker cada pocos minutos; el resultado queda en la bandeja de salida para verlo en superadmin.
    public class EmailTracker
    {
        // Cuánto tiempo se sigue preguntando por un correo que SendPulse todavía no reporta, y por las aperturas de uno ya entregado.
        private static readonly TimeSpan UnknownWindow = TimeSpan.FromDays(7);
        private static readonly TimeSpan OpenWindow = TimeSpan.FromDays(3);
        private const int BatchSize = 100;

        private readonly FelDbContext _db;
        private readonly ICryptoVault _cryptoVault;
        private readonly ILogger<EmailTracker> _logger;

        public EmailTracker(FelDbContext db, ICryptoVault cryptoVault, ILogger<EmailTracker> logger)
        {
            _db = db;
            _cryptoVault = cryptoVault;
            _logger = logger;
        }

        public async Task TrackAsync(CancellationToken ct)
        {
            var settings = await _db.PlatformEmailSettings.AsNoTracking().FirstOrDefaultAsync(s => s.Id == 1, ct);
            if (settings == null || string.IsNullOrWhiteSpace(settings.SendPulseClientId) || string.IsNullOrWhiteSpace(settings.SendPulseSecretEncrypted)) return;
            var secret = _cryptoVault.DecryptPassword(settings.SendPulseSecretEncrypted);

            var now = DateTime.UtcNow;
            var unknownSince = now - UnknownWindow;
            var openSince = now - OpenWindow;
            var pending = await _db.EmailOutbox
                .Where(e => e.Channel == "platform" && e.Status == EmailOutboxStatus.Sent && e.ProviderMessageId != ""
                    && ((e.DeliveryStatus == EmailDeliveryStatus.Unknown && e.SentAt >= unknownSince)
                        || (e.DeliveryStatus == EmailDeliveryStatus.Delivered && e.OpenedAt == null && e.SentAt >= openSince)))
                .OrderBy(e => e.TrackedAt)
                .Take(BatchSize)
                .ToListAsync(ct);

            foreach (var email in pending)
            {
                SendPulseTracking? tracking;
                try
                {
                    tracking = await SendPulseApi.GetTrackingAsync(settings.SendPulseClientId, secret, email.ProviderMessageId, ct);
                }
                catch (SendPulseException ex)
                {
                    _logger.LogWarning("No se pudo consultar el seguimiento en SendPulse: {Error}", ex.Message);
                    return;
                }

                email.TrackedAt = DateTime.UtcNow;
                if (tracking == null) continue;

                if (tracking.Opened && email.OpenedAt == null) email.OpenedAt = tracking.OpenedAt ?? DateTime.UtcNow;
                if (email.DeliveryStatus != EmailDeliveryStatus.Unknown) continue;

                // 2xx: el servidor del destinatario lo aceptó. 5xx: lo rechazó de forma definitiva. 4xx: rechazo temporal, SendPulse reintenta.
                if (tracking.SmtpAnswerCode is >= 200 and < 300)
                {
                    email.DeliveryStatus = EmailDeliveryStatus.Delivered;
                }
                else if (tracking.SmtpAnswerCode >= 500)
                {
                    email.DeliveryStatus = EmailDeliveryStatus.Bounced;
                    email.DeliveryDetail = Truncate($"{tracking.SmtpAnswerCode} {tracking.SmtpAnswerData}".Trim(), 1000);
                    RecordCertificateBounce(email);
                }
                else if (tracking.SmtpAnswerCode >= 400)
                {
                    email.DeliveryDetail = Truncate($"Rechazo temporal {tracking.SmtpAnswerCode} {tracking.SmtpAnswerData}".Trim(), 1000);
                }
            }

            await _db.SaveChangesAsync(ct);
        }

        // Si el correo con el .p12 rebotó, la solicitud de certificado lo muestra igual que un envío fallido.
        private void RecordCertificateBounce(EmailOutbox email)
        {
            if (email.Kind != EmailKind.Certificate || email.CertificateRequestId == null) return;
            _db.CertificateEvents.Add(new CertificateEvent
            {
                Id = Guid.NewGuid(),
                CertificateRequestId = email.CertificateRequestId.Value,
                EventType = CertificateEventType.HolderEmailFailed,
                ActorType = "system",
                OccurredAt = DateTime.UtcNow,
                MetadataJson = JsonSerializer.Serialize(new { email = email.RecipientEmail, channel = email.Channel, error = email.DeliveryDetail })
            });
        }

        private static string Truncate(string value, int max) => value.Length > max ? value[..max] : value;
    }
}
