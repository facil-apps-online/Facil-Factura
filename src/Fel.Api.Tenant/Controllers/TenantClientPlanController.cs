using System;
using System.Linq;
using System.Threading.Tasks;
using Fel.Infrastructure.Data;
using Fel.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Fel.Api.Tenant.Controllers
{
    // Plan y consumo de un cliente del tenant: su plan vigente, el uso del ciclo frente a lo incluido, el excedente, el valor a cobrar y el historial.
    [ApiController]
    [Route("api/tenant/clients/{clientId:guid}/plan")]
    public class TenantClientPlanController : ControllerBase
    {
        private const int HistoryCycles = 6;

        private readonly FelDbContext _dbContext;
        private readonly ClientPlanUsageService _usage;

        public TenantClientPlanController(FelDbContext dbContext, ClientPlanUsageService usage)
        {
            _dbContext = dbContext;
            _usage = usage;
        }

        private Guid GetCurrentTenantId()
        {
            if (Request.Headers.TryGetValue("x-tenant-id", out var tenantIdStr) && Guid.TryParse(tenantIdStr, out var tenantId))
                return tenantId;
            throw new UnauthorizedAccessException("x-tenant-id Header is missing");
        }

        private Task<bool> OwnsClient(Guid clientId) =>
            _dbContext.Clients.AsNoTracking().AnyAsync(c => c.Id == clientId && c.TenantId == GetCurrentTenantId());

        [HttpGet]
        public async Task<IActionResult> Get(Guid clientId)
        {
            if (!await OwnsClient(clientId)) return NotFound("Cliente no encontrado.");
            var now = DateTime.UtcNow;
            var assignment = await _usage.CurrentAssignmentAsync(clientId, now);
            var history = await _usage.GetHistoryAsync(clientId, now, HistoryCycles);
            return Ok(new
            {
                assignment = assignment == null ? null : new
                {
                    assignment.Id, assignment.PlanId, planName = assignment.Plan?.Name, assignment.StartDate, assignment.EndDate,
                    assignment.FixedFeeOverride, assignment.PricePerDocumentOverride
                },
                usage = history.FirstOrDefault(),
                history
            });
        }

        public class AssignRequest
        {
            public Guid PlanId { get; set; }
            public DateTime? StartDate { get; set; }
            public decimal? FixedFeeOverride { get; set; }
            public decimal? PricePerDocumentOverride { get; set; }
        }

        [HttpPut]
        public async Task<IActionResult> Assign(Guid clientId, [FromBody] AssignRequest request)
        {
            var tenantId = GetCurrentTenantId();
            if (!await OwnsClient(clientId)) return NotFound("Cliente no encontrado.");
            var plan = await _dbContext.TenantPlans.AsNoTracking().FirstOrDefaultAsync(p => p.Id == request.PlanId && p.TenantId == tenantId);
            if (plan == null) return BadRequest("El plan no existe.");
            // Un plan archivado no se ofrece para asignaciones nuevas; si el cliente ya lo tiene, se puede volver a guardar sus ajustes.
            if (!plan.IsActive && !await _dbContext.ClientPlanAssignments.AnyAsync(a => a.ClientId == clientId && a.PlanId == plan.Id && a.EndDate == null))
                return BadRequest("El plan está archivado.");
            if (request.FixedFeeOverride is < 0 || request.PricePerDocumentOverride is < 0) return BadRequest("La cuota y el precio no pueden ser negativos.");

            await _usage.AssignAsync(clientId, plan.Id, (request.StartDate ?? DateTime.UtcNow).Date, request.FixedFeeOverride, request.PricePerDocumentOverride);
            await _dbContext.SaveChangesAsync();
            return await Get(clientId);
        }

        // Quita el plan: el vigente termina ayer y el cliente queda sin plan.
        [HttpDelete]
        public async Task<IActionResult> Unassign(Guid clientId)
        {
            if (!await OwnsClient(clientId)) return NotFound("Cliente no encontrado.");
            await _usage.UnassignAsync(clientId, DateTime.UtcNow.Date);
            await _dbContext.SaveChangesAsync();
            return NoContent();
        }
    }
}
