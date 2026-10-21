using System;
using System.Linq;
using System.Threading.Tasks;
using Fel.Core.Entities;
using Fel.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Fel.Api.Tenant.Controllers
{
    // Preferencias generales de alertas y límites de los planes del tenant. Un plan o un cliente pueden pisar la acción al llegar al límite.
    [ApiController]
    [Route("api/tenant/plan-settings")]
    public class TenantPlanSettingsController : ControllerBase
    {
        private readonly FelDbContext _dbContext;

        public TenantPlanSettingsController(FelDbContext dbContext) => _dbContext = dbContext;

        private Guid GetCurrentTenantId()
        {
            if (Request.Headers.TryGetValue("x-tenant-id", out var tenantIdStr) && Guid.TryParse(tenantIdStr, out var tenantId))
                return tenantId;
            throw new UnauthorizedAccessException("x-tenant-id Header is missing");
        }

        public class SettingsRequest
        {
            public bool AlertsEnabled { get; set; } = true;
            public int WarnPercent { get; set; } = 80;
            public bool EmailTenant { get; set; } = true;
            public bool EmailClient { get; set; }
            public PlanExceedAction DefaultExceedAction { get; set; } = PlanExceedAction.Notify;
            // Si el cliente ve su consumo de documentos en su portal (Tenant.ShowUsageToClients). Nulo = no cambia.
            public bool? ShowUsageToClients { get; set; }
        }

        private async Task<object> BuildAsync(Guid tenantId, TenantPlanSettings? row)
        {
            var tenant = await _dbContext.Tenants.AsNoTracking().Where(t => t.Id == tenantId).Select(t => new { t.Email, t.ShowUsageToClients }).FirstAsync();
            var s = row ?? new TenantPlanSettings { TenantId = tenantId };
            return new { s.AlertsEnabled, s.WarnPercent, s.EmailTenant, s.EmailClient, s.DefaultExceedAction, tenantEmail = tenant.Email, showUsageToClients = tenant.ShowUsageToClients };
        }

        [HttpGet]
        public async Task<IActionResult> Get()
        {
            var tenantId = GetCurrentTenantId();
            var row = await _dbContext.TenantPlanSettings.AsNoTracking().FirstOrDefaultAsync(s => s.TenantId == tenantId);
            return Ok(await BuildAsync(tenantId, row));
        }

        [HttpPut]
        public async Task<IActionResult> Put([FromBody] SettingsRequest request)
        {
            var tenantId = GetCurrentTenantId();
            if (request.WarnPercent is < 1 or > 99) return BadRequest("El porcentaje de aviso debe estar entre 1 y 99.");

            var row = await _dbContext.TenantPlanSettings.FirstOrDefaultAsync(s => s.TenantId == tenantId);
            if (row == null)
            {
                row = new TenantPlanSettings { TenantId = tenantId };
                _dbContext.TenantPlanSettings.Add(row);
            }
            row.AlertsEnabled = request.AlertsEnabled;
            row.WarnPercent = request.WarnPercent;
            row.EmailTenant = request.EmailTenant;
            row.EmailClient = request.EmailClient;
            row.DefaultExceedAction = request.DefaultExceedAction;
            row.UpdatedAt = DateTime.UtcNow;
            if (request.ShowUsageToClients.HasValue)
            {
                var tenant = await _dbContext.Tenants.FirstAsync(t => t.Id == tenantId);
                tenant.ShowUsageToClients = request.ShowUsageToClients.Value;
            }
            await _dbContext.SaveChangesAsync();
            return Ok(await BuildAsync(tenantId, row));
        }
    }
}
