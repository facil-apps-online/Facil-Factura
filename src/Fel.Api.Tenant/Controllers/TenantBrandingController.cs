using System;
using System.Linq;
using System.Threading.Tasks;
using Fel.Core.Interfaces;
using Fel.Infrastructure.Data;
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

        [HttpGet("check-slug")]
        public async Task<IActionResult> CheckSlugAvailability([FromQuery] string slug)
        {
            if (string.IsNullOrWhiteSpace(slug)) return Ok(new { isAvailable = false });
            
            Guid.TryParse(Request.Headers["x-tenant-id"].ToString(), out Guid currentTenantId);

            bool exists = await _dbContext.Tenants.AnyAsync(t => t.Slug == slug && t.Id != currentTenantId);
            return Ok(new { isAvailable = !exists });
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
