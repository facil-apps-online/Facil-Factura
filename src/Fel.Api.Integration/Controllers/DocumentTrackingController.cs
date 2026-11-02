using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Fel.Api.Integration.Services;
using Fel.Infrastructure.Data;
using Fel.Infrastructure.Dataico;
using Fel.Infrastructure.Dian;
using Fel.Infrastructure.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Fel.Api.Integration.Controllers
{
    /// <summary>
    /// Consulta de estado y archivos de un documento por su UUID (el <c>id</c> que devuelve la emisión).
    /// Solo se ven los documentos del emisor autenticado por la API Key: uno de otro emisor responde 404,
    /// igual que uno que no existe.
    /// </summary>
    [ApiController]
    [Route("api/co/dian/documents")]
    public class DocumentTrackingController : ControllerBase
    {
        private readonly FelDbContext _context;
        private readonly DocumentPdfService _pdf;
        private readonly DocumentDeliveryService _delivery;

        public DocumentTrackingController(FelDbContext context, DocumentPdfService pdf, DocumentDeliveryService delivery)
        {
            _context = context;
            _pdf = pdf;
            _delivery = delivery;
        }

        private bool TryGetClientId(out Guid clientId)
        {
            clientId = Guid.Empty;
            return HttpContext.Items["ClientId"] is string s && Guid.TryParse(s, out clientId);
        }

        private IActionResult NotFoundDocument() => NotFound(new { code = "DOCUMENT_NOT_FOUND", message = "No existe un documento con ese id para este emisor." });

        /// <summary>
        /// Estado del documento y, si está aprobado, sus archivos en la misma respuesta: el PDF, el XML firmado, la respuesta de la DIAN y el
        /// contenedor AttachedDocument, todos en Base64. Una sola llamada basta; con <c>include=none</c> devuelve solo el estado.
        /// </summary>
        /// <param name="id">UUID devuelto al emitir el documento.</param>
        /// <param name="include"><c>none</c> para no incluir los archivos (consulta liviana). Sin valor se incluyen cuando el documento está aprobado.</param>
        /// <response code="200">Estado del documento y, si aplica, sus archivos.</response>
        /// <response code="404">No existe un documento con ese id para el emisor autenticado.</response>
        [HttpGet("{id:guid}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetStatus(Guid id, [FromQuery] string? include = null)
        {
            if (!TryGetClientId(out var clientId)) return Unauthorized(new { code = "INVALID_API_KEY", message = "No se pudo resolver el emisor autenticado." });

            var doc = await _context.Documents.AsNoTracking().FirstOrDefaultAsync(d => d.Id == id && d.ClientId == clientId);
            if (doc == null) return NotFoundDocument();

            var withFiles = !string.Equals(include, "none", StringComparison.OrdinalIgnoreCase) && doc.Status == "APPROVED";
            object? files = withFiles ? await DocumentFilesBuilder.BuildAsync(_context, _pdf, doc.Id, clientId) : null;

            return Ok(new
            {
                id = doc.Id,
                type = doc.TypeCode,
                number = $"{doc.Prefix}{doc.Number}",
                issueDate = doc.IssueDate,
                total = doc.TotalAmount,
                status = doc.Status,
                cufe = doc.Cufe,
                qr = doc.QrCode,
                dianResponse = DataicoDianOutcome.Readable(doc.DianResponseMessage),
                links = DocumentFilesBuilder.Links(doc.Id),
                files
            });
        }

        /// <summary>
        /// Los archivos del documento en Base64 (mismo contenido que <c>files</c> del GET por id): PDF, XML firmado, respuesta de la DIAN y contenedor
        /// AttachedDocument. Cada archivo que aún no existe llega en null.
        /// </summary>
        /// <param name="id">UUID devuelto al emitir el documento.</param>
        /// <response code="200">Archivos del documento.</response>
        /// <response code="404">No existe un documento con ese id para el emisor autenticado.</response>
        [HttpGet("{id:guid}/files")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetFiles(Guid id)
        {
            if (!TryGetClientId(out var clientId)) return Unauthorized(new { code = "INVALID_API_KEY", message = "No se pudo resolver el emisor autenticado." });

            var doc = await _context.Documents.AsNoTracking().FirstOrDefaultAsync(d => d.Id == id && d.ClientId == clientId);
            if (doc == null) return NotFoundDocument();

            return Ok(new { id = doc.Id, number = $"{doc.Prefix}{doc.Number}", status = doc.Status, files = await DocumentFilesBuilder.BuildAsync(_context, _pdf, doc.Id, clientId) });
        }

        /// <summary>
        /// Descarga directa del PDF del documento, con la plantilla que el emisor eligió en el portal.
        /// </summary>
        /// <param name="id">UUID devuelto al emitir el documento.</param>
        /// <response code="200">El archivo PDF.</response>
        /// <response code="404">No existe el documento para el emisor autenticado.</response>
        /// <response code="422">El PDF no se pudo generar; el mensaje dice por qué (por ejemplo, falta elegir la plantilla).</response>
        [HttpGet("{id:guid}/pdf")]
        [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK, "application/pdf")]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
        public async Task<IActionResult> GetPdf(Guid id)
        {
            if (!TryGetClientId(out var clientId)) return Unauthorized(new { code = "INVALID_API_KEY", message = "No se pudo resolver el emisor autenticado." });

            var doc = await _context.Documents.AsNoTracking().FirstOrDefaultAsync(d => d.Id == id && d.ClientId == clientId);
            if (doc == null) return NotFoundDocument();

            var (pdf, error) = await _pdf.RenderAsync(doc.Id, clientId);
            if (pdf == null) return StatusCode(StatusCodes.Status422UnprocessableEntity, new { code = "PDF_NOT_AVAILABLE", message = error });

            return File(pdf, "application/pdf", $"{doc.Prefix}{doc.Number}.pdf");
        }

        /// <summary>
        /// Descarga directa del XML firmado del documento.
        /// </summary>
        /// <param name="id">UUID devuelto al emitir el documento.</param>
        /// <response code="200">El archivo XML.</response>
        /// <response code="404">No existe el documento para el emisor autenticado, o todavía no tiene XML.</response>
        [HttpGet("{id:guid}/xml")]
        [Produces("application/xml")]
        public async Task<IActionResult> GetXml(Guid id)
        {
            if (!TryGetClientId(out var clientId)) return Unauthorized(new { code = "INVALID_API_KEY", message = "No se pudo resolver el emisor autenticado." });

            var doc = await _context.Documents.AsNoTracking().FirstOrDefaultAsync(d => d.Id == id && d.ClientId == clientId);
            if (doc == null) return NotFoundDocument();

            var stored = await DocumentSignedXmlStore.LoadAsync(_context, doc.Id);
            if (stored == null) return NotFound(new { code = "FILE_NOT_AVAILABLE", message = "El documento todavía no tiene XML firmado." });

            return File(Encoding.UTF8.GetBytes(stored.SignedXml), "application/xml", $"{doc.Prefix}{doc.Number}.xml");
        }

        /// <summary>
        /// Envía por correo al adquirente el PDF propio del integrador. Es el segundo paso de la factura personalizada: se emite con
        /// <c>send_email: false</c> (sin correo), se arma el PDF con el CUFE y el QR y se manda aquí. Con Dataico el correo sale por su servicio de
        /// reenvío; en nativo, por el correo propio como ZIP (contenedor firmado + PDF) con los PDF extra aparte y un tope de 5 envíos por documento cada 24 horas.
        /// </summary>
        /// <param name="id">UUID devuelto al emitir el documento.</param>
        /// <param name="body"><c>to</c> (opcional: por defecto el correo del adquirente), <c>cc</c> (hasta 3, solo nativo), <c>subject</c> (solo nativo),
        /// <c>pdf_base64</c> (opcional: sin él se usa el de la plantilla del emisor) y <c>attachments</c> (PDF extra con <c>name</c> y <c>content_base64</c>).</param>
        /// <response code="200">Envío hecho (Dataico) o en cola (nativo).</response>
        /// <response code="400">Datos inválidos: correo, PDF o adjuntos.</response>
        /// <response code="404">No existe un documento con ese id para el emisor autenticado.</response>
        /// <response code="422">El documento no está aprobado, el proveedor rechazó el envío o no hay a quién enviarlo.</response>
        /// <response code="429">Tope de envíos por documento alcanzado (nativo).</response>
        [HttpPost("{id:guid}/send")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
        [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
        public async Task<IActionResult> Send(Guid id, [FromBody] SendDocumentBody? body)
        {
            if (!TryGetClientId(out var clientId)) return Unauthorized(new { code = "INVALID_API_KEY", message = "No se pudo resolver el emisor autenticado." });
            if (!await _context.Documents.AnyAsync(d => d.Id == id && d.ClientId == clientId)) return NotFoundDocument();
            body ??= new SendDocumentBody();

            var problems = new List<string>();
            byte[]? pdf = null;
            if (!string.IsNullOrWhiteSpace(body.pdf_base64))
            {
                pdf = DecodePdf(body.pdf_base64, "pdf_base64", problems);
            }
            var extras = new List<(string Name, byte[] Content)>();
            var total = pdf?.Length ?? 0;
            for (var i = 0; i < (body.attachments?.Count ?? 0); i++)
            {
                var a = body.attachments![i];
                var name = (a.name ?? string.Empty).Trim();
                if (name.Length == 0) { problems.Add($"attachments[{i}].name: es obligatorio."); continue; }
                if (!name.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)) name += ".pdf";
                var content = DecodePdf(a.content_base64, $"attachments[{i}].content_base64", problems);
                if (content == null) continue;
                total += content.Length;
                extras.Add((Path.GetFileName(name), content));
            }
            if (total > Fel.Core.Entities.DocumentAttachment.MaxTotalBytes)
                problems.Add($"El PDF y los adjuntos suman más de {Fel.Core.Entities.DocumentAttachment.MaxTotalBytes / (1024 * 1024)} MB.");
            if (problems.Count > 0)
                return BadRequest(new { code = "VALIDATION_ERROR", message = "El envío tiene datos inválidos. Revisa el detalle.", details = problems });

            var result = await _delivery.SendAsync(id, clientId, new DocumentSendRequest(body.to, body.cc ?? new List<string>(), body.subject, pdf, extras));
            if (result.Success)
                return Ok(new { id, status = "SENT", provider = result.Provider, message = result.Message, emailId = result.EmailId, warnings = result.Warnings });

            var status = result.Code switch
            {
                "DOCUMENT_NOT_FOUND" => StatusCodes.Status404NotFound,
                "SEND_LIMIT" => StatusCodes.Status429TooManyRequests,
                "INVALID_EMAIL" or "TOO_MANY_RECIPIENTS" => StatusCodes.Status400BadRequest,
                _ => StatusCodes.Status422UnprocessableEntity
            };
            return StatusCode(status, new { code = result.Code, message = result.Message });
        }

        // Solo se aceptan PDF reales: Base64 válido y encabezado %PDF.
        private static byte[]? DecodePdf(string? base64, string field, List<string> problems)
        {
            if (string.IsNullOrWhiteSpace(base64)) { problems.Add($"{field}: es obligatorio."); return null; }
            try
            {
                var bytes = Convert.FromBase64String(base64.Trim());
                if (bytes.Length < 5 || bytes[0] != '%' || bytes[1] != 'P' || bytes[2] != 'D' || bytes[3] != 'F') { problems.Add($"{field}: no es un PDF (debe empezar con %PDF)."); return null; }
                return bytes;
            }
            catch (FormatException)
            {
                problems.Add($"{field}: no es Base64 válido.");
                return null;
            }
        }

        private static string? ToBase64(string? text) => string.IsNullOrEmpty(text) ? null : Convert.ToBase64String(Encoding.UTF8.GetBytes(text));
    }

    /// <summary>Cuerpo de <c>POST documents/{id}/send</c>.</summary>
    public class SendDocumentBody
    {
        public string? to { get; set; }
        public List<string>? cc { get; set; }
        public string? subject { get; set; }
        public string? pdf_base64 { get; set; }
        public List<SendAttachment>? attachments { get; set; }
    }

    public class SendAttachment
    {
        public string? name { get; set; }
        public string? content_base64 { get; set; }
    }
}
