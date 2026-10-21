using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Fel.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Fel.Api.Tenant.Controllers
{
    // Avisos de consumo de los clientes del tenant (llegaron al porcentaje de aviso o al límite de su plan, o el sistema los bloqueó o desbloqueó).
    [ApiController]
    [Route("api/tenant/plan-alerts")]
    public class TenantPlanAlertsController : ControllerBase
    {
        private readonly FelDbContext _dbContext;

        public TenantPlanAlertsController(FelDbContext dbContext) => _dbContext = dbContext;

        private Guid GetCurrentTenantId()
        {
            if (Request.Headers.TryGetValue("x-tenant-id", out var tenantIdStr) && Guid.TryParse(tenantIdStr, out var tenantId))
                return tenantId;
            throw new UnauthorizedAccessException("x-tenant-id Header is missing");
        }

        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] bool unseenOnly = true, [FromQuery] int limit = 30)
        {
            var tenantId = GetCurrentTenantId();
            var query = _dbContext.PlanAlerts.AsNoTracking().Where(a => a.TenantId == tenantId);
            if (unseenOnly) query = query.Where(a => a.SeenAt == null);
            var unseen = await _dbContext.PlanAlerts.CountAsync(a => a.TenantId == tenantId && a.SeenAt == null);
            var items = await query.OrderByDescending(a => a.CreatedAt).Take(Math.Clamp(limit, 1, 200))
                .Select(a => new
                {
                    a.Id, a.ClientId,
                    clientName = !string.IsNullOrEmpty(a.Client!.CommercialName) ? a.Client.CommercialName : a.Client.CompanyName,
                    a.Kind, a.Documents, a.Included, a.Message, a.CreatedAt, seen = a.SeenAt != null
                })
                .ToListAsync();
            return Ok(new { unseen, items });
        }

        public class SeenRequest
        {
            // Vacío = todos los pendientes del tenant.
            public List<Guid>? Ids { get; set; }
        }

        [HttpPost("seen")]
        public async Task<IActionResult> MarkSeen([FromBody] SeenRequest request)
        {
            var tenantId = GetCurrentTenantId();
            var query = _dbContext.PlanAlerts.Where(a => a.TenantId == tenantId && a.SeenAt == null);
            if (request.Ids is { Count: > 0 }) query = query.Where(a => request.Ids.Contains(a.Id));
            var now = DateTime.UtcNow;
            foreach (var a in await query.ToListAsync()) a.SeenAt = now;
            await _dbContext.SaveChangesAsync();
            return NoContent();
        }
    }
}
