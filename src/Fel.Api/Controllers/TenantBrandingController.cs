using System;
using System.Linq;
using System.Threading.Tasks;
using Fel.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Fel.Api.Controllers
{
    [ApiController]
    [Route("api/tenant/branding")]
    public class TenantBrandingController : ControllerBase
    {
        private readonly FelDbContext _dbContext;

        public TenantBrandingController(FelDbContext dbContext)
        {
            _dbContext = dbContext;
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
                    t.PrimaryColorDark
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
            
            await _dbContext.SaveChangesAsync();

            return Ok(new { Message = "Branding actualizado exitosamente" });
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
    }
}
