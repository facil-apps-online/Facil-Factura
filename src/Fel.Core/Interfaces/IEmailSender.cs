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

        // "client-smtp" | "tenant-smtp" | "platform" | "none" — para trazabilidad en logs.
        public string TransportUsed { get; set; } = "none";

        public static EmailSendResult Ok(string transport) => new() { Success = true, TransportUsed = transport };
        public static EmailSendResult Fail(string error, string transport = "none") => new() { Success = false, ErrorMessage = error, TransportUsed = transport };
    }

    /// <summary>
    /// Envía correo con adjuntos resolviendo el transporte por prioridad: SMTP propio del Client,
    /// si no tiene, SMTP del Tenant (client.Tenant debe venir cargado), si tampoco, la plataforma
    /// (Brevo vía Core). Usado hoy solo por el reenvío de documentos del flujo nativo DIAN (el
    /// flujo Dataico tiene su propio mecanismo de reenvío contra la API de Dataico, ver
    /// IDataicoApiService.SendCustomDocumentPdfAsync).
    /// </summary>
    public interface IEmailSender
    {
        Task<EmailSendResult> SendAsync(
            Client client, string recipientEmail, string subject, string bodyHtml,
            IReadOnlyList<EmailAttachment>? attachments = null, CancellationToken ct = default);
    }
}
