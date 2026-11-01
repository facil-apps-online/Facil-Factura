using System;
using System.Linq;
using System.Threading.Tasks;
using Fel.Core.Interfaces;
using Fel.Infrastructure.Data;
using Fel.Infrastructure.Email;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Fel.Api.Tenant.Controllers
{
    [ApiController]
    [Route("api/tenant/branding")]
    public class TenantBrandingController : ControllerBase
    {
        private const string ApiBaseUrl = "https://api.facil-factura.pro";
        private readonly FelDbContext _dbContext;
        private readonly IPublicFileStorageService _fileStorage;

        public TenantBrandingController(FelDbContext dbContext, IPublicFileStorageService fileStorage)
        {
            _dbContext = dbContext;
            _fileStorage = fileStorage;
        }

        // Endpoint público para el cliente final (usa Slug en la URL)
        [HttpGet("{slug}")]
        public async Task<IActionResult> GetBrandingBySlug(string slug)
        {
            var tenant = await _dbContext.Tenants
                .Where(t => t.Slug == slug && t.IsActive)
                .Select(t => new
                {
                    t.Name,
                    t.CommercialName,
                    t.LogoLightUrl,
                    t.LogoDarkUrl,
                    t.PrimaryColorLight,
                    t.PrimaryColorDark
                })
                .FirstOrDefaultAsync();

            if (tenant == null)
            {
                return NotFound(new { Message = "Micrositio no encontrado o inactivo." });
            }

            return Ok(tenant);
        }

        // Endpoint privado para el portal de Tenant (usa Header Auth)
        [HttpGet("my-branding")]
        public async Task<IActionResult> GetMyBranding()
        {
            if (!Request.Headers.TryGetValue("x-tenant-id", out var tenantIdStr) || !Guid.TryParse(tenantIdStr, out Guid tenantId))
            {
                return Unauthorized("Tenant ID no proporcionado o inválido.");
            }

            var tenant = await _dbContext.Tenants
                .Where(t => t.Id == tenantId && t.IsActive)
                .Select(t => new
                {
                    t.Slug,
                    t.Name,
                    t.CommercialName,
                    t.LogoLightUrl,
                    t.LogoDarkUrl,
                    t.PrimaryColorLight,
                    t.PrimaryColorDark,
                    BillingMode = t.BillingMode.ToString(),
                    t.ShowUsageToClients
                })
                .FirstOrDefaultAsync();

            if (tenant == null) return NotFound(new { Message = "Tenant no encontrado." });

            return Ok(tenant);
        }

        [HttpPut("my-branding")]
        public async Task<IActionResult> UpdateMyBranding([FromBody] UpdateBrandingRequest request)
        {
            if (!Request.Headers.TryGetValue("x-tenant-id", out var tenantIdStr) || !Guid.TryParse(tenantIdStr, out Guid tenantId))
            {
                return Unauthorized("Tenant ID no proporcionado o inválido.");
            }

            var tenant = await _dbContext.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId && t.IsActive);
            if (tenant == null) return NotFound(new { Message = "Tenant no encontrado." });

            if (!string.IsNullOrEmpty(request.Slug) && request.Slug != tenant.Slug)
            {
                var existingSlug = await _dbContext.Tenants.AnyAsync(t => t.Slug == request.Slug && t.Id != tenant.Id);
                if (existingSlug)
                {
                    return BadRequest("El Slug ya está en uso por otra cuenta.");
                }
                tenant.Slug = request.Slug;
            }

            tenant.PrimaryColorLight = request.PrimaryColorLight;
            tenant.LogoLightUrl = request.LogoLightUrl;
            tenant.ShowUsageToClients = request.ShowUsageToClients;

            await _dbContext.SaveChangesAsync();

            return Ok(new { Message = "Branding actualizado exitosamente" });
        }

        [HttpPost("logo")]
        public async Task<IActionResult> UploadLogo(IFormFile file)
        {
            if (!Request.Headers.TryGetValue("x-tenant-id", out var tenantIdStr) || !Guid.TryParse(tenantIdStr, out Guid tenantId))
            {
                return Unauthorized("Tenant ID no proporcionado o inválido.");
            }

            if (file == null || file.Length == 0)
            {
                return BadRequest("Debes adjuntar un archivo de imagen.");
            }

            if (file.Length > 5 * 1024 * 1024)
            {
                return BadRequest("La imagen no puede superar 5MB.");
            }

            var tenant = await _dbContext.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId && t.IsActive);
            if (tenant == null) return NotFound(new { Message = "Tenant no encontrado." });

            try
            {
                using var stream = file.OpenReadStream();
                var relativeKey = await _fileStorage.SaveFileAsync("logos", tenantId, stream, file.FileName);
                tenant.LogoLightUrl = $"{ApiBaseUrl}/api/tenant/files/{relativeKey}";
                await _dbContext.SaveChangesAsync();

                return Ok(new { logoLightUrl = tenant.LogoLightUrl });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // Colores de los correos con los que sus clientes entregan los documentos, cuando el cliente no definió los suyos. Nulos = los de la plataforma.
        [HttpGet("email-colors")]
        public async Task<IActionResult> GetEmailColors()
        {
            if (!Request.Headers.TryGetValue("x-tenant-id", out var tenantIdStr) || !Guid.TryParse(tenantIdStr, out Guid tenantId))
                return Unauthorized("Tenant ID no proporcionado o inválido.");

            var (light, dark) = await DocumentEmailColors.TenantColorsAsync(_dbContext, tenantId);
            return Ok(new
            {
                EmailColorLight = light,
                EmailColorDark = dark,
                InheritedEmailColor = DocumentEmailColors.Resolve(await DocumentEmailColors.PlatformColorsAsync(_dbContext))
            });
        }

        [HttpPut("email-colors")]
        public async Task<IActionResult> UpdateEmailColors([FromBody] EmailColorsRequest request)
        {
            if (!Request.Headers.TryGetValue("x-tenant-id", out var tenantIdStr) || !Guid.TryParse(tenantIdStr, out Guid tenantId))
                return Unauthorized("Tenant ID no proporcionado o inválido.");
            var tenant = await _dbContext.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId && t.IsActive);
            if (tenant == null) return NotFound(new { Message = "Tenant no encontrado." });

            if (!request.TryNormalize(out var light, out var dark)) return BadRequest(new { Message = "Los colores deben tener el formato #rrggbb." });
            tenant.EmailColorLight = light;
            tenant.EmailColorDark = dark;
            await _dbContext.SaveChangesAsync();
            return Ok(new { Message = "Colores guardados." });
        }

        // Cómo se vería el correo de un documento con esos colores (sin guardarlos), con un documento de ejemplo.
        [HttpPost("email-preview")]
        public async Task<IActionResult> PreviewEmail([FromBody] EmailColorsRequest request)
        {
            if (!Request.Headers.TryGetValue("x-tenant-id", out var tenantIdStr) || !Guid.TryParse(tenantIdStr, out Guid tenantId))
                return Unauthorized("Tenant ID no proporcionado o inválido.");
            var tenant = await _dbContext.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Id == tenantId && t.IsActive);
            if (tenant == null) return NotFound(new { Message = "Tenant no encontrado." });

            if (!request.TryNormalize(out var light, out var dark)) return BadRequest(new { Message = "Los colores deben tener el formato #rrggbb." });
            var background = DocumentEmailColors.Resolve((light, dark), await DocumentEmailColors.PlatformColorsAsync(_dbContext));
            var html = DocumentEmailTemplate.Build(new DocumentEmailModel(
                string.IsNullOrWhiteSpace(tenant.CommercialName) ? tenant.Name : tenant.CommercialName, null, null, null, null,
                string.IsNullOrWhiteSpace(tenant.LogoLightUrl) ? null : tenant.LogoLightUrl,
                "factura electrónica de venta", "FE1001", DateTime.Today, 1250000m, "COP", background));
            return Ok(new { html });
        }

        [HttpGet("check-slug")]
        public async Task<IActionResult> CheckSlugAvailability([FromQuery] string slug)
        {
            if (string.IsNullOrWhiteSpace(slug)) return Ok(new { isAvailable = false });
            
            Guid.TryParse(Request.Headers["x-tenant-id"].ToString(), out Guid currentTenantId);

            bool exists = await _dbContext.Tenants.AnyAsync(t => t.Slug == slug && t.Id != currentTenantId);
            return Ok(new { isAvailable = !exists });
        }
    }

    public class EmailColorsRequest
    {
        // Vacío o nulo = heredar los de la plataforma.
        public string? ColorLight { get; set; }
        public string? ColorDark { get; set; }

        // Normaliza los dos colores; falla si alguno viene con texto que no es #rrggbb.
        public bool TryNormalize(out string? light, out string? dark)
        {
            light = DocumentEmailColors.Normalize(ColorLight);
            dark = DocumentEmailColors.Normalize(ColorDark);
            return (string.IsNullOrWhiteSpace(ColorLight) || light != null) && (string.IsNullOrWhiteSpace(ColorDark) || dark != null);
        }
    }

    public class UpdateBrandingRequest
    {
        public string Slug { get; set; } = string.Empty;
        public string PrimaryColorLight { get; set; } = string.Empty;
        public string LogoLightUrl { get; set; } = string.Empty;
        public bool ShowUsageToClients { get; set; }
    }
}
