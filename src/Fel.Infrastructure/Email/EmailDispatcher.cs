using System.Text.Json;
using Fel.Core.Entities;
using Fel.Core.Interfaces;
using Fel.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Fel.Infrastructure.Email
{
    // Envía una fila de la bandeja de salida por el canal que corresponda (SMTP del cliente → SMTP del tenant → Brevo) y deja el
    // resultado en la fila. Lo ejecuta el worker al recibir el aviso y en el barrido de seguridad.
    public class EmailDispatcher
    {
        public const int MaxAttempts = 5;
        private static readonly TimeSpan[] Backoff = { TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(15), TimeSpan.FromHours(1) };
        // Un correo en "Sending" por más de esto se da por interrumpido (reinicio del worker) y se vuelve a tomar.
        private static readonly TimeSpan StaleSending = TimeSpan.FromMinutes(10);

        private readonly FelDbContext _db;
        private readonly IEmailSender _sender;
        private readonly ICryptoVault _cryptoVault;
        private readonly IEmailSignal _signal;
        private readonly ILogger<EmailDispatcher> _logger;

        public EmailDispatcher(FelDbContext db, IEmailSender sender, ICryptoVault cryptoVault, IEmailSignal signal, ILogger<EmailDispatcher> logger)
        {
            _db = db;
            _sender = sender;
            _cryptoVault = cryptoVault;
            _signal = signal;
            _logger = logger;
        }

        // Ids que ya toca enviar y no tuvieron aviso (o cuyo envío se interrumpió o falló y toca reintentar).
        public async Task<List<Guid>> FindDueAsync(CancellationToken ct)
        {
            var now = DateTime.UtcNow;
            var stale = now - StaleSending;
            return await _db.EmailOutbox.AsNoTracking()
                .Where(e => ((e.Status == EmailOutboxStatus.Pending || e.Status == EmailOutboxStatus.Failed) && e.NextAttemptAt <= now)
                            || (e.Status == EmailOutboxStatus.Sending && e.UpdatedAt < stale))
                .OrderBy(e => e.NextAttemptAt)
                .Select(e => e.Id)
                .Take(100)
                .ToListAsync(ct);
        }

        public async Task ProcessAsync(Guid id, CancellationToken ct)
        {
            var now = DateTime.UtcNow;
            var stale = now - StaleSending;
            // Toma el correo de forma atómica: si dos avisos o el barrido llegan a la vez, solo uno lo envía.
            var claimed = await _db.EmailOutbox
                .Where(e => e.Id == id
                    && (((e.Status == EmailOutboxStatus.Pending || e.Status == EmailOutboxStatus.Failed) && e.NextAttemptAt <= now)
                        || (e.Status == EmailOutboxStatus.Sending && e.UpdatedAt < stale)))
                .ExecuteUpdateAsync(s => s
                    .SetProperty(e => e.Status, EmailOutboxStatus.Sending)
                    .SetProperty(e => e.UpdatedAt, now)
                    .SetProperty(e => e.Attempts, e => e.Attempts + 1), ct);
            if (claimed == 0) return;

            var email = await _db.EmailOutbox.Include(e => e.Client).ThenInclude(c => c!.Tenant).FirstAsync(e => e.Id == id, ct);

            EmailSendResult result;
            try
            {
                if (email.Client == null) result = EmailSendResult.Fail("El correo no tiene un cliente de origen.");
                else
                {
                    var body = email.Sensitive ? _cryptoVault.DecryptPassword(email.BodyHtml) : email.BodyHtml;
                    var attachments = string.IsNullOrEmpty(email.AttachmentsJson)
                        ? null
                        : JsonSerializer.Deserialize<List<StoredAttachment>>(email.AttachmentsJson)!
                            .Select(a => new EmailAttachment { FileName = a.FileName, ContentType = a.ContentType, Content = Convert.FromBase64String(a.ContentBase64) })
                            .ToList();
                    result = await _sender.SendAsync(email.Client, email.Kind, email.RecipientEmail, email.Subject, body, attachments, ct);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Error preparando el correo {EmailId}.", id);
                result = EmailSendResult.Fail(ex.Message);
            }

            email.UpdatedAt = DateTime.UtcNow;
            email.Channel = result.TransportUsed;
            if (result.Success)
            {
                email.Status = EmailOutboxStatus.Sent;
                email.SentAt = DateTime.UtcNow;
                email.ProviderMessageId = result.ProviderMessageId;
                email.LastError = string.Empty;
                // Lo enviado no se conserva: ni los adjuntos ni, en los sensibles, el cuerpo (trae la contraseña del certificado).
                email.AttachmentsJson = null;
                if (email.Sensitive) email.BodyHtml = string.Empty;
                RecordCertificateEvent(email, CertificateEventType.HolderEmailSent);
                await _db.SaveChangesAsync(ct);
                await ReleaseDependentsAsync(email.Id, ct);
                return;
            }

            email.LastError = Truncate(result.ErrorMessage ?? "No se pudo enviar el correo.", 1000);
            if (email.Attempts >= MaxAttempts)
            {
                email.Status = EmailOutboxStatus.Dead;
                RecordCertificateEvent(email, CertificateEventType.HolderEmailFailed);
                await _db.SaveChangesAsync(ct);
                await CancelDependentsAsync(email.Id, ct);
            }
            else
            {
                email.Status = EmailOutboxStatus.Failed;
                email.NextAttemptAt = DateTime.UtcNow + Backoff[Math.Min(email.Attempts - 1, Backoff.Length - 1)];
                await _db.SaveChangesAsync(ct);
            }
            _logger.LogWarning("Correo {EmailId} a {Recipient} no enviado (intento {Attempt}): {Error}", id, email.RecipientEmail, email.Attempts, email.LastError);
        }

        // La contraseña del certificado solo sale cuando el correo con el archivo quedó enviado.
        private async Task ReleaseDependentsAsync(Guid emailId, CancellationToken ct)
        {
            var ids = await _db.EmailOutbox.Where(e => e.DependsOnId == emailId && e.Status == EmailOutboxStatus.Held).Select(e => e.Id).ToListAsync(ct);
            if (ids.Count == 0) return;
            var now = DateTime.UtcNow;
            await _db.EmailOutbox.Where(e => ids.Contains(e.Id) && e.Status == EmailOutboxStatus.Held)
                .ExecuteUpdateAsync(s => s.SetProperty(e => e.Status, EmailOutboxStatus.Pending).SetProperty(e => e.NextAttemptAt, now).SetProperty(e => e.UpdatedAt, now), ct);
            foreach (var id in ids) await _signal.PublishAsync(id);
        }

        private async Task CancelDependentsAsync(Guid emailId, CancellationToken ct)
        {
            var now = DateTime.UtcNow;
            await _db.EmailOutbox.Where(e => e.DependsOnId == emailId && e.Status == EmailOutboxStatus.Held)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(e => e.Status, EmailOutboxStatus.Cancelled)
                    .SetProperty(e => e.BodyHtml, string.Empty)
                    .SetProperty(e => e.LastError, "No se envió porque el correo con el archivo no pudo enviarse.")
                    .SetProperty(e => e.UpdatedAt, now), ct);
        }

        // El resultado del correo con el .p12 queda en el historial de la solicitud de certificado.
        private void RecordCertificateEvent(EmailOutbox email, CertificateEventType type)
        {
            if (email.Kind != EmailKind.Certificate || email.CertificateRequestId == null) return;
            _db.CertificateEvents.Add(new CertificateEvent
            {
                Id = Guid.NewGuid(),
                CertificateRequestId = email.CertificateRequestId.Value,
                EventType = type,
                ActorType = "system",
                OccurredAt = DateTime.UtcNow,
                MetadataJson = JsonSerializer.Serialize(new { email = email.RecipientEmail, channel = email.Channel, error = type == CertificateEventType.HolderEmailFailed ? email.LastError : null })
            });
        }

        private static string Truncate(string value, int max) => value.Length > max ? value[..max] : value;
    }
}
