using Fel.Core.Entities;
using Fel.Core.Interfaces;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using MimeKit;

namespace Fel.Infrastructure.Services
{
    // Resuelve el transporte de correo por prioridad: SMTP propio del Client, si no está
    // configurado el del Tenant (client.Tenant debe venir cargado), y si tampoco, la plataforma
    // (Brevo vía Core). El tercer nivel depende de que Core soporte adjuntos en
    // queue_platform_email_with_attachment, que a la fecha de este código no existe todavía del
    // lado de Core — mientras tanto ese nivel falla con un mensaje claro en vez de enviar el correo
    // sin el documento adjunto.
    public class SmtpEmailSender : IEmailSender
    {
        private readonly ICryptoService _cryptoService;
        private readonly ICoreApiClient _coreApiClient;
        private readonly ILogger<SmtpEmailSender> _logger;

        public SmtpEmailSender(ICryptoService cryptoService, ICoreApiClient coreApiClient, ILogger<SmtpEmailSender> logger)
        {
            _cryptoService = cryptoService;
            _coreApiClient = coreApiClient;
            _logger = logger;
        }

        public async Task<EmailSendResult> SendAsync(
            Client client, string recipientEmail, string subject, string bodyHtml,
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

            if (attachments != null && attachments.Count > 0)
            {
                return EmailSendResult.Fail(
                    "No hay un SMTP propio configurado (ni del cliente ni del tenant) y el envío por la plataforma todavía no soporta adjuntos.",
                    "platform");
            }

            var coreResult = await _coreApiClient.QueuePlatformEmailAsync(
                recipientEmail, "invoice_resend",
                new Dictionary<string, string> { ["subject"] = subject, ["body_html"] = bodyHtml },
                tenant?.CoreTenantId, ct);

            if (coreResult.IsSuccess) return EmailSendResult.Ok("platform");
            return EmailSendResult.Fail(coreResult.Error ?? "No se pudo encolar el correo en la plataforma.", "platform");
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
