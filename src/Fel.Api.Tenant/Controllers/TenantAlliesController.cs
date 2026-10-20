using System;
using System.Linq;
using System.Threading.Tasks;
using Fel.Infrastructure.Data;
using Fel.Infrastructure.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Fel.Api.Tenant.Controllers
{
    // Aliados del tenant: los tenants que lo tienen como padre (Tenant.ParentTenantId). El padre puede bloquearlos: nadie del aliado ni de sus
    // clientes (ni de los aliados de ese aliado) entra al portal ni emite por API, y al levantarlo todo vuelve. Es un bloqueo distinto del de la
    // plataforma (ver Tenant.PlatformBlockedAt): el padre no puede levantar el de la plataforma ni al revés. Solo se maneja a los aliados directos;
    // los de un aliado los maneja ese aliado, y bloquear al aliado ya los alcanza.
    [ApiController]
    [Route("api/tenant/allies")]
    public class TenantAlliesController : ControllerBase
    {
        private readonly FelDbContext _dbContext;

        public TenantAlliesController(FelDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        private Guid GetCurrentTenantId()
        {
            if (Request.Headers.TryGetValue("x-tenant-id", out var tenantIdStr) && Guid.TryParse(tenantIdStr, out var tenantId))
                return tenantId;
            throw new UnauthorizedAccessException("x-tenant-id Header is missing");
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var tenantId = GetCurrentTenantId();
            var allies = await _dbContext.Tenants.AsNoTracking()
                .Where(t => t.ParentTenantId == tenantId)
                .OrderBy(t => t.CommercialName)
                .Select(t => new
                {
                    t.Id,
                    Name = !string.IsNullOrEmpty(t.CommercialName) ? t.CommercialName : t.Name,
                    t.Slug,
                    t.IsActive,
                    Clients = t.Clients.Count(c => c.IsActive && !c.IsDeveloperSandbox),
                    Blocked = t.ParentBlockedAt != null,
                    BlockedAt = t.ParentBlockedAt,
                    BlockReason = t.ParentBlockReason,
                    PlatformBlocked = t.PlatformBlockedAt != null,
                    t.CanViewPlatformBilling
                })
                .ToListAsync();
            return Ok(allies);
        }

        [HttpPut("{id:guid}/block")]
        public async Task<IActionResult> SetBlock(Guid id, [FromBody] AllyBlockRequest request)
        {
            var tenantId = GetCurrentTenantId();
            var ally = await _dbContext.Tenants.FirstOrDefaultAsync(t => t.Id == id && t.ParentTenantId == tenantId);
            if (ally == null) return NotFound("Aliado no encontrado.");

            if (request.Blocked)
            {
                var reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim();
                if (reason is { Length: > 500 }) return BadRequest("El motivo no puede pasar de 500 caracteres.");
                ally.ParentBlockedAt = DateTime.UtcNow;
                ally.ParentBlockReason = reason;
                ally.ParentBlockedBy = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value;
            }
            else
            {
                ally.ParentBlockedAt = null;
                ally.ParentBlockReason = null;
                ally.ParentBlockedBy = null;
            }

            await _dbContext.SaveChangesAsync();
            TenantBlocks.Invalidate();
            return Ok(new { ally.Id, Blocked = ally.ParentBlockedAt != null, ally.ParentBlockReason });
        }

        // Si el aliado puede ver en su portal lo que debe a la plataforma (por defecto no).
        [HttpPut("{id:guid}/platform-billing")]
        public async Task<IActionResult> SetPlatformBillingVisibility(Guid id, [FromBody] AllyBillingVisibilityRequest request)
        {
            var tenantId = GetCurrentTenantId();
            var ally = await _dbContext.Tenants.FirstOrDefaultAsync(t => t.Id == id && t.ParentTenantId == tenantId);
            if (ally == null) return NotFound("Aliado no encontrado.");

            ally.CanViewPlatformBilling = request.Visible;
            await _dbContext.SaveChangesAsync();
            return Ok(new { ally.Id, ally.CanViewPlatformBilling });
        }

        public class AllyBillingVisibilityRequest
        {
            public bool Visible { get; set; }
        }

        public class AllyBlockRequest
        {
            public bool Blocked { get; set; }
            public string? Reason { get; set; }
        }
    }
}
