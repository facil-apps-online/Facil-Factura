using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using Fel.Core.Entities;
using Fel.Core.Interfaces;
using Fel.Infrastructure.Data;
using Fel.Infrastructure.Dataico;
using Fel.Infrastructure.Dian;
using Fel.Infrastructure.Email;
using Microsoft.EntityFrameworkCore;

namespace Fel.Infrastructure.Services
{
    // Lo que el integrador pide enviar: su propio PDF (si no lo trae se usa la plantilla del emisor), los destinatarios y adjuntos extra en PDF.
    public sealed record DocumentSendRequest(
        string? To, IReadOnlyList<string> Cc, string? Subject, byte[]? Pdf, IReadOnlyList<(string Name, byte[] Content)> Extras);

    public sealed record DocumentSendResult(bool Success, string? Code, string? Message, string? Provider, IReadOnlyList<string> Warnings, Guid? EmailId = null);

    // Entrega por correo de un documento ya aprobado, con el PDF que el integrador quiere mostrarle a su cliente. El camino depende de quién emitió:
    // con Dataico se usa su PUT de reenvío con el PDF en base64 (Dataico lo reenvía y no lo guarda); en nativo sale por la bandeja de salida propia
    // (SMTP del cliente, del tenant o SendPulse) como el ZIP del contenedor AttachedDocument más el PDF, con los adjuntos extra aparte.
    public class DocumentDeliveryService
    {
        // Tope de envíos por documento en 24 horas, solo en el método nativo (Dataico controla sus propios reenvíos).
        public const int NativeDailySendLimit = 5;
        public const int MaxCc = 3;

        private readonly FelDbContext _db;
        private readonly IEmailOutbox _outbox;
        private readonly AttachedDocumentService _attachedDocuments;
        private readonly DocumentPdfService _pdf;
        private readonly IDataicoApiService _dataico;
        private readonly ICryptoService _crypto;

        public DocumentDeliveryService(FelDbContext db, IEmailOutbox outbox, AttachedDocumentService attachedDocuments, DocumentPdfService pdf,
            IDataicoApiService dataico, ICryptoService crypto)
        {
            _db = db;
            _outbox = outbox;
            _attachedDocuments = attachedDocuments;
            _pdf = pdf;
            _dataico = dataico;
            _crypto = crypto;
        }

        private static DocumentSendResult Fail(string code, string message) => new(false, code, message, null, Array.Empty<string>());

        public async Task<DocumentSendResult> SendAsync(Guid documentId, Guid clientId, DocumentSendRequest request)
        {
            var document = await _db.Documents.AsNoTracking().Include(d => d.Customer).FirstOrDefaultAsync(d => d.Id == documentId && d.ClientId == clientId);
            if (document == null) return Fail("DOCUMENT_NOT_FOUND", "No existe un documento con ese id para este emisor.");
            if (document.Status != "APPROVED") return Fail("DOCUMENT_NOT_APPROVED", $"Solo se envían documentos aprobados; este está en estado {document.Status}.");
            if (document.TypeCode is "DS" or "DS-AJUSTE" || document.TypeCode.StartsWith("NE", StringComparison.OrdinalIgnoreCase))
                return Fail("NOT_SUPPORTED", "El envío por correo de este tipo de documento todavía no está disponible por la API.");

            var to = (request.To ?? document.Customer?.Email ?? string.Empty).Trim();
            if (to.Length == 0) return Fail("NO_RECIPIENT", "No hay a quién enviarlo: indica \"to\" o registra el correo del adquirente.");
            if (!IsEmail(to)) return Fail("INVALID_EMAIL", $"\"{to}\" no es un correo válido.");
            foreach (var cc in request.Cc)
                if (!IsEmail(cc)) return Fail("INVALID_EMAIL", $"\"{cc}\" no es un correo válido.");
            if (request.Cc.Count > MaxCc) return Fail("TOO_MANY_RECIPIENTS", $"Máximo {MaxCc} destinatarios en copia.");

            var client = await _db.Clients.Include(c => c.Integrator).Include(c => c.Tenant).FirstOrDefaultAsync(c => c.Id == clientId);
            if (client == null) return Fail("ISSUER_NOT_FOUND", "Emisor no encontrado.");

            var pdf = request.Pdf;
            if (pdf == null)
            {
                var (rendered, error) = await _pdf.RenderAsync(documentId, clientId);
                if (rendered == null) return Fail("PDF_NOT_AVAILABLE", $"No se envió PDF propio y no se pudo generar el del emisor: {error}");
                pdf = rendered;
            }

            return client.Integrator.Code == "DATAICO"
                ? await SendViaDataicoAsync(document, client, to, request, pdf)
                : await SendNativeAsync(document, client, to, request, pdf);
        }

        private static bool IsEmail(string value) =>
            value.Length is > 3 and <= 254 && value.Count(c => c == '@') == 1 && !value.StartsWith('@') && !value.EndsWith('@') && !value.Contains(' ') && value[(value.IndexOf('@') + 1)..].Contains('.');

        // Dataico: PUT con el PDF propio y los adjuntos. Solo acepta un destinatario; subject y cc no los admite.
        private async Task<DocumentSendResult> SendViaDataicoAsync(Document document, Client client, string to, DocumentSendRequest request, byte[] pdf)
        {
            if (string.IsNullOrEmpty(document.DataicoDocumentId))
                return Fail("NOT_SUPPORTED", "Este documento no tiene guardado su identificador de Dataico; no se puede enviar por este medio.");

            var warnings = new List<string>();
            if (request.Cc.Count > 0) warnings.Add("Dataico solo admite un destinatario: se ignoraron las copias (cc).");
            if (!string.IsNullOrWhiteSpace(request.Subject)) warnings.Add("Dataico arma el asunto del correo: se ignoró subject.");

            var credentials = DataicoDocumentMapper.ToCredentials(client, _crypto);
            var stored = await new DocumentAttachmentService(_db).LoadForSendAsync(document.Id);
            var extras = stored.Concat(request.Extras).ToList();
            var result = await _dataico.SendCustomDocumentPdfAsync(document.DataicoDocumentId, document.TypeCode, pdf, to, credentials, extras);
            if (!result.Success)
                return new DocumentSendResult(false, "PROVIDER_REJECTED", $"Dataico rechazó el envío: {(string.IsNullOrWhiteSpace(result.ErrorMessage) ? result.RawResponse : result.ErrorMessage)}", "DATAICO", warnings);
            return new DocumentSendResult(true, null, $"Documento enviado a {to} por Dataico.", "DATAICO", warnings);
        }

        // Nativo: ZIP (contenedor AttachedDocument + PDF, con los nombres del anexo técnico) y los PDF extra, por la bandeja de salida.
        private async Task<DocumentSendResult> SendNativeAsync(Document document, Client client, string to, DocumentSendRequest request, byte[] pdf)
        {
            var since = DateTime.UtcNow.AddHours(-24);
            var recent = await _db.EmailOutbox.CountAsync(e => e.DocumentId == document.Id && e.Kind == EmailKind.Document && e.CreatedAt >= since);
            if (recent >= NativeDailySendLimit)
                return Fail("SEND_LIMIT", $"Este documento ya se envió {NativeDailySendLimit} veces en las últimas 24 horas; vuelve a intentarlo más tarde.");

            var stored = await DocumentSignedXmlStore.LoadAsync(_db, document.Id);
            if (stored == null) return Fail("FILE_NOT_AVAILABLE", "Este documento se emitió antes de que se guardara su XML firmado; no se puede enviar por este medio. Contacta soporte.");
            var (container, containerError) = await _attachedDocuments.EnsureAsync(document, client);
            if (container == null) return Fail("FILE_NOT_AVAILABLE", $"No se pudo preparar el documento para el adquirente. {containerError}");

            var year = stored.SignedAt.Year;
            var sequence = stored.FileSequence > 0 ? stored.FileSequence : client.DianFileSequence;
            var adName = DianFileNaming.BuildName(DianFileNaming.AttachedDocument, client.TaxId, sequence, year, "xml");
            var adPdfName = DianFileNaming.BuildName(DianFileNaming.AttachedDocument, client.TaxId, sequence, year, "pdf");
            var zipName = DianFileNaming.BuildName(DianFileNaming.Zip, client.TaxId, sequence, year, "zip");
            var zip = BuildZip(adName, container, adPdfName, pdf);
            var subject = string.IsNullOrWhiteSpace(request.Subject) ? AttachedDocumentBuilder.BuildEmailSubject(stored.SignedXml) : request.Subject.Trim();

            var attachments = new List<EmailAttachment> { new() { FileName = zipName, Content = zip, ContentType = "application/zip" } };
            foreach (var (name, content) in request.Extras)
                attachments.Add(new EmailAttachment { FileName = name, Content = content, ContentType = "application/pdf" });

            var background = DocumentEmailColors.Resolve(
                (client.EmailColorLight, client.EmailColorDark),
                await DocumentEmailColors.TenantColorsAsync(_db, client.TenantId),
                await DocumentEmailColors.PlatformColorsAsync(_db));
            var bodyHtml = DocumentEmailTemplate.Build(new DocumentEmailModel(
                string.IsNullOrWhiteSpace(client.CommercialName) ? client.CompanyName : client.CommercialName,
                string.IsNullOrWhiteSpace(client.VerificationDigit) ? client.TaxId : $"{client.TaxId}-{client.VerificationDigit}",
                client.Address, client.Phone, client.Email,
                !string.IsNullOrWhiteSpace(client.LogoLightUrl) ? client.LogoLightUrl : client.LogoDarkUrl,
                document.TypeCode switch { "NC" => "nota crédito electrónica", "ND" => "nota débito electrónica", "DE-POS" => "documento equivalente electrónico POS", _ => "factura electrónica de venta" },
                $"{document.Prefix}{document.Number}", document.IssueDate, document.TotalAmount, document.Currency, background));

            // Una fila por destinatario (el correo propio solo lleva uno); cada una cuenta para el tope diario.
            var recipients = new[] { to }.Concat(request.Cc).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (recent + recipients.Count > NativeDailySendLimit)
                return Fail("SEND_LIMIT", $"Con estos destinatarios se pasaría del tope de {NativeDailySendLimit} envíos por documento en 24 horas.");
            var queued = recipients.Select(r => _outbox.Add(new OutboundEmail
            {
                Kind = EmailKind.Document, Client = client, RecipientEmail = r, Subject = subject, BodyHtml = bodyHtml, Attachments = attachments, DocumentId = document.Id
            })).ToArray();
            await _db.SaveChangesAsync();
            await _outbox.NotifyAsync(queued);

            return new DocumentSendResult(true, null, $"Envío en cola para {string.Join(", ", recipients)}.", "NATIVE", Array.Empty<string>(), queued[0].Id);
        }

        private static byte[] BuildZip(string xmlName, string xml, string pdfName, byte[] pdf)
        {
            using var stream = new MemoryStream();
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
            {
                using (var writer = new StreamWriter(archive.CreateEntry(xmlName, CompressionLevel.Optimal).Open())) writer.Write(xml);
                using var pdfStream = archive.CreateEntry(pdfName, CompressionLevel.Optimal).Open();
                pdfStream.Write(pdf, 0, pdf.Length);
            }
            return stream.ToArray();
        }
    }
}
