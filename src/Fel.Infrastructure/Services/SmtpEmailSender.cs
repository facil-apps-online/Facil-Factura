using Fel.Infrastructure.Email;
using Fel.Core.Entities;
using Fel.Core.Interfaces;
using Fel.Infrastructure.Data;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MimeKit;

namespace Fel.Infrastructure.Services
{
    // Resuelve el transporte de correo por prioridad: SMTP propio del Client, si no está configurado el del Tenant (client.Tenant debe
    // venir cargado), y si tampoco, SendPulse directo desde la plataforma con adjuntos. Por Core no sale nada.
    public class SmtpEmailSender : IEmailSender
    {

        private readonly ICryptoService _cryptoService;
        private readonly ICryptoVault _cryptoVault;
        private readonly FelDbContext _db;
        private readonly ILogger<SmtpEmailSender> _logger;

        public SmtpEmailSender(ICryptoService cryptoService, ICryptoVault cryptoVault, FelDbContext db, ILogger<SmtpEmailSender> logger)
        {
            _cryptoService = cryptoService;
            _cryptoVault = cryptoVault;
            _db = db;
            _logger = logger;
        }

        public async Task<EmailSendResult> SendAsync(
            Client client, EmailKind kind, string recipientEmail, string subject, string bodyHtml,
            IReadOnlyList<EmailAttachment>? attachments = null, CancellationToken ct = default)
        {
            if (HasSmtpConfigured(client.SmtpHost, client.SmtpUser, client.SmtpPasswordEncrypted))
            {
                return await SendViaSmtpAsync(
                    "client-smtp", client.SmtpHost!, client.SmtpPort ?? 587, client.SmtpUser!, client.SmtpPasswordEncrypted!,
                    client.SmtpUseSsl, client.SmtpFromEmail ?? client.SmtpUser!, client.SmtpFromName ?? client.CompanyName,
                    recipientEmail, subject, bodyHtml, attachments, ct);
            }

            var tenant = client.Tenant;
            if (tenant != null && HasSmtpConfigured(tenant.SmtpHost, tenant.SmtpUser, tenant.SmtpPasswordEncrypted))
            {
                return await SendViaSmtpAsync(
                    "tenant-smtp", tenant.SmtpHost!, tenant.SmtpPort ?? 587, tenant.SmtpUser!, tenant.SmtpPasswordEncrypted!,
                    tenant.SmtpUseSsl, tenant.SmtpFromEmail ?? tenant.SmtpUser!, tenant.SmtpFromName ?? tenant.Name,
                    recipientEmail, subject, bodyHtml, attachments, ct);
            }

            return await SendViaSendPulseAsync(settings => ResolveSender(settings, client, kind), recipientEmail, subject, bodyHtml, attachments, ct);
        }

        // Remitente según el tipo de correo (ver el backlog de correo propio): el nombre visible es el de quien origina el correo y las
        // respuestas van a su contacto; la dirección es siempre de facil-factura.pro, que es el dominio autenticado en SendPulse.
        private static (string Name, string Email, string? ReplyTo) ResolveSender(PlatformEmailSettings settings, Client client, EmailKind kind)
        {
            var tenant = client.Tenant;
            var tenantName = tenant != null ? (!string.IsNullOrWhiteSpace(tenant.CommercialName) ? tenant.CommercialName : tenant.Name) : settings.PlatformFromName;
            var clientName = !string.IsNullOrWhiteSpace(client.CommercialName) ? client.CommercialName : client.CompanyName;
            return kind switch
            {
                EmailKind.Document => (clientName, settings.DocumentsFromEmail, NullIfBlank(client.Email)),
                EmailKind.Certificate or EmailKind.CertificatePassword => (tenantName, settings.CertificatesFromEmail, NullIfBlank(tenant?.Email)),
                EmailKind.Notification => (tenantName, settings.NotificationsFromEmail, NullIfBlank(tenant?.Email)),
                _ => (settings.PlatformFromName, settings.PlatformFromEmail, NullIfBlank(settings.SupportEmail))
            };
        }

        private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

        public Task<EmailSendResult> SendPlatformAsync(
            Tenant? tenant, EmailKind kind, string recipientEmail, string subject, string bodyHtml, CancellationToken ct = default)
            => SendViaSendPulseAsync(settings => ResolvePlatformSender(settings, tenant), recipientEmail, subject, bodyHtml, null, ct);

        // Sin cliente de por medio: el nombre visible es el del tenant dueño de la cuenta (la marca que reconoce quien recibe el correo).
        private static (string Name, string Email, string? ReplyTo) ResolvePlatformSender(PlatformEmailSettings settings, Tenant? tenant)
        {
            var name = tenant != null ? (!string.IsNullOrWhiteSpace(tenant.CommercialName) ? tenant.CommercialName : tenant.Name) : settings.PlatformFromName;
            return (name, settings.PlatformFromEmail, NullIfBlank(settings.SupportEmail) ?? NullIfBlank(tenant?.Email));
        }

        private async Task<EmailSendResult> SendViaSendPulseAsync(
            Func<PlatformEmailSettings, (string Name, string Email, string? ReplyTo)> resolveSender, string recipientEmail,
            string subject, string bodyHtml, IReadOnlyList<EmailAttachment>? attachments, CancellationToken ct)
        {
            try
            {
                var settings = await _db.PlatformEmailSettings.AsNoTracking().FirstOrDefaultAsync(s => s.Id == 1, ct);
                if (settings == null || string.IsNullOrWhiteSpace(settings.SendPulseClientId) || string.IsNullOrWhiteSpace(settings.SendPulseSecretEncrypted))
                    return EmailSendResult.Fail("La plataforma no tiene configuradas las credenciales de SendPulse (superadmin → Correo de la plataforma).", "platform");

                // Tope por hora del plan: lo que pasa de ahí no es un fallo, espera a que salga el correo más antiguo de la ventana.
                if (settings.MaxPerHour > 0)
                {
                    var windowStart = DateTime.UtcNow.AddHours(-1);
                    var sentInWindow = await _db.EmailOutbox.AsNoTracking()
                        .Where(e => e.Channel == "platform" && e.Status == EmailOutboxStatus.Sent && e.SentAt >= windowStart)
                        .OrderBy(e => e.SentAt).Select(e => e.SentAt!.Value).ToListAsync(ct);
                    if (sentInWindow.Count >= settings.MaxPerHour)
                        return EmailSendResult.Throttled(sentInWindow[sentInWindow.Count - settings.MaxPerHour].AddHours(1).AddSeconds(5));
                }

                var secret = _cryptoVault.DecryptPassword(settings.SendPulseSecretEncrypted);
                var (name, email, replyTo) = resolveSender(settings);
                var messageId = await SendPulseApi.SendAsync(settings.SendPulseClientId, secret, name, email, replyTo, recipientEmail, subject, bodyHtml, attachments, ct);
                return EmailSendResult.Ok("platform", messageId);
            }
            catch (SendPulseException ex)
            {
                _logger.LogWarning("SendPulse no envió el correo a {Recipient}: {Error}", recipientEmail, ex.Message);
                return EmailSendResult.Fail(ex.Message, "platform");
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Error enviando correo vía SendPulse a {Recipient}.", recipientEmail);
                return EmailSendResult.Fail(ex.Message, "platform");
            }
        }

        private static bool HasSmtpConfigured(string? host, string? user, string? passwordEncrypted)
            => !string.IsNullOrWhiteSpace(host) && !string.IsNullOrWhiteSpace(user) && !string.IsNullOrWhiteSpace(passwordEncrypted);

        private async Task<EmailSendResult> SendViaSmtpAsync(
            string transportLabel, string host, int port, string user, string passwordEncrypted, bool useSsl,
            string fromEmail, string fromName, string recipientEmail, string subject, string bodyHtml,
            IReadOnlyList<EmailAttachment>? attachments, CancellationToken ct)
        {
            try
            {
                var password = _cryptoService.Decrypt(passwordEncrypted);

                var message = new MimeMessage();
                message.From.Add(new MailboxAddress(fromName, fromEmail));
                message.To.Add(MailboxAddress.Parse(recipientEmail));
                message.Subject = subject;

                var builder = new BodyBuilder { HtmlBody = bodyHtml };
                foreach (var attachment in attachments ?? Array.Empty<EmailAttachment>())
                {
                    builder.Attachments.Add(attachment.FileName, attachment.Content, ContentType.Parse(attachment.ContentType));
                }
                message.Body = builder.ToMessageBody();

                using var smtp = new SmtpClient();
                var socketOptions = useSsl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTlsWhenAvailable;
                await smtp.ConnectAsync(host, port, socketOptions, ct);
                await smtp.AuthenticateAsync(user, password, ct);
                await smtp.SendAsync(message, ct);
                await smtp.DisconnectAsync(true, ct);

                return EmailSendResult.Ok(transportLabel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error enviando correo vía {Transport} ({Host}) a {Recipient}.", transportLabel, host, recipientEmail);
                return EmailSendResult.Fail(ex.Message, transportLabel);
            }
        }
    }
}
