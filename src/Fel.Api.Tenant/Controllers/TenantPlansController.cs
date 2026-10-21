using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Fel.Core.Entities;
using Fel.Infrastructure.Data;
using Fel.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Fel.Api.Tenant.Controllers
{
    // Planes comerciales que el Tenant ofrece a sus clientes: cuota fija, documentos incluidos (o ilimitados), precio por documento o por excedente, qué
    // cuenta como documento y cada cuánto se cobra. Archivar no borra: los clientes con historial siguen apuntando a su plan.
    [ApiController]
    [Route("api/tenant/plans")]
    public class TenantPlansController : ControllerBase
    {
        private readonly FelDbContext _dbContext;
        private readonly ClientPlanUsageService _usage;

        public TenantPlansController(FelDbContext dbContext, ClientPlanUsageService usage)
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

        public class PlanRequest
        {
            public string Name { get; set; } = string.Empty;
            public string? Description { get; set; }
            public PlanCycle Cycle { get; set; } = PlanCycle.Monthly;
            public decimal FixedFee { get; set; }
            public PlanFeeTiming FeeTiming { get; set; } = PlanFeeTiming.Advance;
            // Nulo = ilimitados.
            public int? IncludedDocuments { get; set; }
            public decimal PricePerDocument { get; set; }
            public PlanScope Scope { get; set; } = PlanScope.PerClient;
            public bool CountApproved { get; set; } = true;
            public bool CountRejected { get; set; } = true;
            public bool CountInProcess { get; set; }
            public bool CountInvoices { get; set; } = true;
            public bool CountNotes { get; set; } = true;
            public bool CountSupportDocuments { get; set; } = true;
            public bool CountPayroll { get; set; } = true;
            // Nulo = la acción general del tenant.
            public PlanExceedAction? ExceedAction { get; set; }
            // Productos de la empresa emisora para las líneas de la factura de este plan. Nulo = los del tenant.
            public Guid? FeeProductId { get; set; }
            public Guid? ExtraProductId { get; set; }
        }

        private static object ToDto(TenantPlan p, int clients) => new
        {
            p.Id, p.Name, p.Description, p.Cycle, p.FixedFee, p.FeeTiming, p.IncludedDocuments, p.PricePerDocument, p.Scope, p.IsActive, p.ExceedAction, p.FeeProductId, p.ExtraProductId,
            countApproved = p.CountedStatuses.HasFlag(PlanCountedStatuses.Approved),
            countRejected = p.CountedStatuses.HasFlag(PlanCountedStatuses.Rejected),
            countInProcess = p.CountedStatuses.HasFlag(PlanCountedStatuses.InProcess),
            countInvoices = p.CountedTypes.HasFlag(PlanCountedTypes.Invoices),
            countNotes = p.CountedTypes.HasFlag(PlanCountedTypes.Notes),
            countSupportDocuments = p.CountedTypes.HasFlag(PlanCountedTypes.SupportDocuments),
            countPayroll = p.CountedTypes.HasFlag(PlanCountedTypes.Payroll),
            clients
        };

        private static string? Validate(PlanRequest r)
        {
            if (string.IsNullOrWhiteSpace(r.Name)) return "El plan necesita un nombre.";
            if (r.Name.Trim().Length > 100) return "El nombre no puede pasar de 100 caracteres.";
            if (r.Description is { Length: > 500 }) return "La descripción no puede pasar de 500 caracteres.";
            if (r.FixedFee < 0 || r.PricePerDocument < 0) return "La cuota y el precio por documento no pueden ser negativos.";
            if (r.IncludedDocuments is < 0) return "Los documentos incluidos no pueden ser negativos.";
            if (!r.CountApproved && !r.CountRejected && !r.CountInProcess) return "Elige al menos un estado de documento que cuente.";
            if (!r.CountInvoices && !r.CountNotes && !r.CountSupportDocuments && !r.CountPayroll) return "Elige al menos un tipo de documento que cuente.";
            return null;
        }

        private static void Apply(TenantPlan p, PlanRequest r)
        {
            p.Name = r.Name.Trim();
            p.Description = string.IsNullOrWhiteSpace(r.Description) ? null : r.Description.Trim();
            p.Cycle = r.Cycle;
            p.FixedFee = r.FixedFee;
            p.FeeTiming = r.FeeTiming;
            p.IncludedDocuments = r.IncludedDocuments;
            p.PricePerDocument = r.PricePerDocument;
            p.Scope = r.Scope;
            p.ExceedAction = r.ExceedAction;
            p.FeeProductId = r.FeeProductId;
            p.ExtraProductId = r.ExtraProductId;
            p.CountedStatuses = (r.CountApproved ? PlanCountedStatuses.Approved : 0) | (r.CountRejected ? PlanCountedStatuses.Rejected : 0) | (r.CountInProcess ? PlanCountedStatuses.InProcess : 0);
            p.CountedTypes = (r.CountInvoices ? PlanCountedTypes.Invoices : 0) | (r.CountNotes ? PlanCountedTypes.Notes : 0)
                             | (r.CountSupportDocuments ? PlanCountedTypes.SupportDocuments : 0) | (r.CountPayroll ? PlanCountedTypes.Payroll : 0);
            p.UpdatedAt = DateTime.UtcNow;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] bool includeArchived = false)
        {
            var tenantId = GetCurrentTenantId();
            var today = DateTime.UtcNow.Date;
            var plans = await _dbContext.TenantPlans.AsNoTracking()
                .Where(p => p.TenantId == tenantId && (includeArchived || p.IsActive))
                .OrderByDescending(p => p.IsActive).ThenBy(p => p.Name)
                .ToListAsync();
            var counts = await _dbContext.ClientPlanAssignments.AsNoTracking()
                .Where(a => a.Plan!.TenantId == tenantId && a.StartDate <= today && (a.EndDate == null || a.EndDate >= today))
                .GroupBy(a => a.PlanId)
                .Select(g => new { PlanId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.PlanId, x => x.Count);
            return Ok(plans.Select(p => ToDto(p, counts.GetValueOrDefault(p.Id))));
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] PlanRequest request)
        {
            var tenantId = GetCurrentTenantId();
            var error = Validate(request);
            if (error != null) return BadRequest(error);
            var name = request.Name.Trim();
            if (await _dbContext.TenantPlans.AnyAsync(p => p.TenantId == tenantId && p.Name == name)) return BadRequest("Ya tienes un plan con ese nombre.");

            var plan = new TenantPlan { Id = Guid.NewGuid(), TenantId = tenantId, CreatedAt = DateTime.UtcNow };
            Apply(plan, request);
            _dbContext.TenantPlans.Add(plan);
            await _dbContext.SaveChangesAsync();
            return Ok(ToDto(plan, 0));
        }

        [HttpPut("{id:guid}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] PlanRequest request)
        {
            var tenantId = GetCurrentTenantId();
            var error = Validate(request);
            if (error != null) return BadRequest(error);
            var plan = await _dbContext.TenantPlans.FirstOrDefaultAsync(p => p.Id == id && p.TenantId == tenantId);
            if (plan == null) return NotFound("Plan no encontrado.");
            var name = request.Name.Trim();
            if (await _dbContext.TenantPlans.AnyAsync(p => p.TenantId == tenantId && p.Name == name && p.Id != id)) return BadRequest("Ya tienes un plan con ese nombre.");

            Apply(plan, request);
            await _dbContext.SaveChangesAsync();
            var today = DateTime.UtcNow.Date;
            var clients = await _dbContext.ClientPlanAssignments.CountAsync(a => a.PlanId == id && a.StartDate <= today && (a.EndDate == null || a.EndDate >= today));
            return Ok(ToDto(plan, clients));
        }

        // Archivar: el plan deja de ofrecerse para nuevas asignaciones, pero los clientes que ya lo tienen lo conservan.
        [HttpPost("{id:guid}/archive")]
        public Task<IActionResult> Archive(Guid id) => SetActive(id, false);

        [HttpPost("{id:guid}/restore")]
        public Task<IActionResult> Restore(Guid id) => SetActive(id, true);

        private async Task<IActionResult> SetActive(Guid id, bool active)
        {
            var tenantId = GetCurrentTenantId();
            var plan = await _dbContext.TenantPlans.FirstOrDefaultAsync(p => p.Id == id && p.TenantId == tenantId);
            if (plan == null) return NotFound("Plan no encontrado.");
            plan.IsActive = active;
            plan.UpdatedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();
            return NoContent();
        }

        public class BulkAssignRequest
        {
            public List<Guid> ClientIds { get; set; } = new();
            public Guid PlanId { get; set; }
            public DateTime? StartDate { get; set; }
        }

        // Asigna un plan a varios clientes a la vez (desde la lista de clientes).
        [HttpPost("assign")]
        public async Task<IActionResult> AssignBulk([FromBody] BulkAssignRequest request)
        {
            var tenantId = GetCurrentTenantId();
            var plan = await _dbContext.TenantPlans.AsNoTracking().FirstOrDefaultAsync(p => p.Id == request.PlanId && p.TenantId == tenantId && p.IsActive);
            if (plan == null) return BadRequest("El plan no existe o está archivado.");
            var ids = request.ClientIds.Distinct().ToList();
            if (ids.Count == 0) return BadRequest("Selecciona al menos un cliente.");
            var valid = await _dbContext.Clients.AsNoTracking().Where(c => c.TenantId == tenantId && ids.Contains(c.Id)).Select(c => c.Id).ToListAsync();
            if (valid.Count != ids.Count) return BadRequest("Algún cliente no pertenece a este tenant.");

            var start = (request.StartDate ?? DateTime.UtcNow).Date;
            foreach (var clientId in valid) await _usage.AssignAsync(clientId, plan.Id, start, null, null);
            await _dbContext.SaveChangesAsync();
            return Ok(new { assigned = valid.Count });
        }
    }
}
