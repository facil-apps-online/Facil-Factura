using System;
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

        public DocumentTrackingController(FelDbContext context, DocumentPdfService pdf)
        {
            _context = context;
            _pdf = pdf;
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

        private static string? ToBase64(string? text) => string.IsNullOrEmpty(text) ? null : Convert.ToBase64String(Encoding.UTF8.GetBytes(text));
    }
}
