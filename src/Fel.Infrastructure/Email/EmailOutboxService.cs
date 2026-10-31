using System.Text.Json;
using Fel.Core.Entities;
using Fel.Core.Interfaces;
using Fel.Infrastructure.Data;

namespace Fel.Infrastructure.Email
{
    public class EmailOutboxService : IEmailOutbox
    {
        private readonly FelDbContext _db;
        private readonly ICryptoVault _cryptoVault;
        private readonly IEmailSignal _signal;

        public EmailOutboxService(FelDbContext db, ICryptoVault cryptoVault, IEmailSignal signal)
        {
            _db = db;
            _cryptoVault = cryptoVault;
            _signal = signal;
        }

        public EmailOutbox Add(OutboundEmail email)
        {
            var row = new EmailOutbox
            {
                Id = Guid.NewGuid(),
                Kind = email.Kind,
                // Con dependencia espera (Held) a que el otro correo quede enviado.
                Status = email.DependsOnId != null ? EmailOutboxStatus.Held : EmailOutboxStatus.Pending,
                TenantId = email.Client.TenantId,
                ClientId = email.Client.Id,
                RecipientEmail = email.RecipientEmail,
                Subject = email.Subject.Length > 500 ? email.Subject[..500] : email.Subject,
                BodyHtml = email.Sensitive ? _cryptoVault.EncryptPassword(email.BodyHtml) : email.BodyHtml,
                Sensitive = email.Sensitive,
                AttachmentsJson = email.Attachments is { Count: > 0 }
                    ? JsonSerializer.Serialize(email.Attachments.Select(a => new StoredAttachment(a.FileName, a.ContentType, Convert.ToBase64String(a.Content))))
                    : null,
                DocumentId = email.DocumentId,
                CertificateRequestId = email.CertificateRequestId,
                DependsOnId = email.DependsOnId
            };
            _db.EmailOutbox.Add(row);
            return row;
        }

        public async Task NotifyAsync(params EmailOutbox[] emails)
        {
            foreach (var email in emails.Where(e => e.Status == EmailOutboxStatus.Pending))
                await _signal.PublishAsync(email.Id);
        }
    }

    public record StoredAttachment(string FileName, string ContentType, string ContentBase64);
}
