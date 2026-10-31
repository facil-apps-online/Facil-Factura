using Fel.Core.Entities;

namespace Fel.Core.Interfaces
{
    public class EmailAttachment
    {
        public string FileName { get; set; } = string.Empty;
        public byte[] Content { get; set; } = Array.Empty<byte>();
        public string ContentType { get; set; } = "application/octet-stream";
    }

    public class EmailSendResult
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }

        // "client-smtp" | "tenant-smtp" | "platform" (Brevo) | "none" — para trazabilidad en logs.
        public string TransportUsed { get; set; } = "none";
        public string ProviderMessageId { get; set; } = string.Empty;

        public static EmailSendResult Ok(string transport, string providerMessageId = "") => new() { Success = true, TransportUsed = transport, ProviderMessageId = providerMessageId };
        public static EmailSendResult Fail(string error, string transport = "none") => new() { Success = false, ErrorMessage = error, TransportUsed = transport };
    }

    /// <summary>
    /// Transporte del correo: lo usa solo el worker al enviar una fila de la bandeja de salida (<see cref="IEmailOutbox"/>). Resuelve el
    /// canal por prioridad: SMTP propio del Client, si no tiene, SMTP del Tenant (client.Tenant debe venir cargado), si tampoco, Brevo
    /// directo desde la plataforma (con adjuntos). Nunca sale por Core. El flujo Dataico tiene su propio mecanismo de reenvío contra la
    /// API de Dataico, ver IDataicoApiService.SendCustomDocumentPdfAsync.
    /// </summary>
    public interface IEmailSender
    {
        Task<EmailSendResult> SendAsync(
            Client client, EmailKind kind, string recipientEmail, string subject, string bodyHtml,
            IReadOnlyList<EmailAttachment>? attachments = null, CancellationToken ct = default);
    }
}
