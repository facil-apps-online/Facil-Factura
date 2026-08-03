using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Fel.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Fel.Api.Integration.Controllers
{
    [ApiController]
    [Route("api/documents")]
    public class DocumentTrackingController : ControllerBase
    {
        private readonly FelDbContext _context;

        public DocumentTrackingController(FelDbContext context)
        {
            _context = context;
        }
        [HttpGet("{trackId}/status")]
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
                FilesUrl = $"/api/documents/{trackId}/files"
            });
        }

        [HttpGet("{trackId}/files")]
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
