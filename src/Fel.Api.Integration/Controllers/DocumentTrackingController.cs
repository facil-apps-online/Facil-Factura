using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Fel.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Fel.Api.Integration.Controllers
{
    /// <summary>
    /// Consulta de estado y archivos de los documentos electrónicos que el tenant ya envió por
    /// cualquiera de los endpoints de recepción de este API (facturas, notas, nómina, etc.).
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

        /// <summary>
        /// Devuelve el estado actual de un documento por su <c>TrackingId</c> (el que se recibió
        /// como respuesta al encolarlo).
        /// </summary>
        /// <param name="trackId">TrackingId devuelto al recibir el documento.</param>
        /// <response code="200">Estado del documento, respuesta de la DIAN y CUFE si ya fue asignado.</response>
        /// <response code="401">No se pudo resolver el Tenant autenticado.</response>
        /// <response code="404">No existe un documento con ese TrackingId para este Tenant.</response>
        [HttpGet("{trackId}/status")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetStatus(string trackId)
        {
            // Extraer TenantId validado por el Middleware HMAC (Seguridad)
            var tenantIdStr = HttpContext.Items["TenantId"]?.ToString();
            
            if (!Guid.TryParse(tenantIdStr, out var tenantId))
                return Unauthorized();

            var doc = await _context.Documents.FirstOrDefaultAsync(d => d.TrackingId == trackId && d.Client.TenantId == tenantId);
            
            if (doc == null)
                return NotFound(new { message = "Documento no encontrado o no pertenece al Tenant." });

            return Ok(new
            {
                TrackId = doc.TrackingId,
                Status = doc.Status,
                DianResponse = doc.DianResponseMessage ?? "Procesando",
                Cufe = doc.Cufe ?? "",
                FilesUrl = $"/api/co/dian/documents/{trackId}/files"
            });
        }

        /// <summary>
        /// Devuelve el XML y PDF del documento (codificados en Base64) una vez fue procesado por la DIAN.
        /// </summary>
        /// <param name="trackId">TrackingId devuelto al recibir el documento.</param>
        /// <response code="200">Archivos del documento en Base64.</response>
        /// <response code="401">No se pudo resolver el Tenant autenticado.</response>
        /// <response code="404">No existe un documento con ese TrackingId para este Tenant.</response>
        [HttpGet("{trackId}/files")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetFilesBase64(string trackId)
        {
            var tenantIdStr = HttpContext.Items["TenantId"]?.ToString();
            
            if (!Guid.TryParse(tenantIdStr, out var tenantId))
                return Unauthorized();

            var doc = await _context.Documents.FirstOrDefaultAsync(d => d.TrackingId == trackId && d.Client.TenantId == tenantId);
            
            if (doc == null)
                return NotFound();

            // Para producción real, leer desde S3 usando doc.XmlUrl y doc.PdfUrl.
            // MVP: Se devuelven strings que la BD/App haya guardado o generado.
            return Ok(new
            {
                TrackId = doc.TrackingId,
                PdfBase64 = doc.PdfUrl ?? "PdfNoDisponible",
                XmlBase64 = doc.XmlUrl ?? "XmlNoDisponible",
                ZipBase64 = ""
            });
        }
    }
}
