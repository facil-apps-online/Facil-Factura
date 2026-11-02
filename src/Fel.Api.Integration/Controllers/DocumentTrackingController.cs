using System;
using System.Text;
using System.Threading.Tasks;
using Fel.Infrastructure.Data;
using Fel.Infrastructure.Dian;
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

        public DocumentTrackingController(FelDbContext context)
        {
            _context = context;
        }

        private bool TryGetClientId(out Guid clientId)
        {
            clientId = Guid.Empty;
            return HttpContext.Items["ClientId"] is string s && Guid.TryParse(s, out clientId);
        }

        private IActionResult NotFoundDocument() => NotFound(new { code = "DOCUMENT_NOT_FOUND", message = "No existe un documento con ese id para este emisor." });

        /// <summary>
        /// Estado actual del documento: número, estado ante la DIAN, respuesta y CUFE una vez asignado.
        /// </summary>
        /// <param name="id">UUID devuelto al emitir el documento.</param>
        /// <response code="200">Estado del documento.</response>
        /// <response code="404">No existe un documento con ese id para el emisor autenticado.</response>
        [HttpGet("{id:guid}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetStatus(Guid id)
        {
            if (!TryGetClientId(out var clientId)) return Unauthorized(new { code = "INVALID_API_KEY", message = "No se pudo resolver el emisor autenticado." });

            var doc = await _context.Documents.AsNoTracking().FirstOrDefaultAsync(d => d.Id == id && d.ClientId == clientId);
            if (doc == null) return NotFoundDocument();

            return Ok(new
            {
                id = doc.Id,
                type = doc.TypeCode,
                number = $"{doc.Prefix}{doc.Number}",
                issueDate = doc.IssueDate,
                total = doc.TotalAmount,
                status = doc.Status,
                dianResponse = doc.DianResponseMessage,
                cufe = doc.Cufe,
                files = $"/api/co/dian/documents/{doc.Id}/files"
            });
        }

        /// <summary>
        /// Archivos del documento en Base64 real, listos para guardar: el XML firmado que se envió a la DIAN, la
        /// respuesta de la DIAN y el contenedor AttachedDocument. Cada archivo que aún no existe llega en null.
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

            var stored = await DocumentSignedXmlStore.LoadAsync(_context, doc.Id);
            return Ok(new
            {
                id = doc.Id,
                number = $"{doc.Prefix}{doc.Number}",
                status = doc.Status,
                xmlBase64 = ToBase64(stored?.SignedXml),
                dianResponseBase64 = ToBase64(stored?.ApplicationResponse),
                attachedDocumentBase64 = ToBase64(stored?.AttachedDocument)
            });
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
