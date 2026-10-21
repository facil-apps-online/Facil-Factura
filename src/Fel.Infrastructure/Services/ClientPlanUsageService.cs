using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Fel.Core.Entities;
using Fel.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Fel.Infrastructure.Services
{
    public class PlanUsageUnit
    {
        public Guid? BranchId { get; set; }
        public string Name { get; set; } = string.Empty;
        public int Documents { get; set; }
        public int? Included { get; set; }
        public int Extra { get; set; }
        public decimal Fee { get; set; }
        public decimal ExtraAmount { get; set; }
        public decimal Total => Fee + ExtraAmount;
    }

    public class PlanUsage
    {
        public Guid AssignmentId { get; set; }
        public Guid PlanId { get; set; }
        public string PlanName { get; set; } = string.Empty;
        public PlanCycle Cycle { get; set; }
        public PlanFeeTiming FeeTiming { get; set; }
        public PlanScope Scope { get; set; }
        public DateTime PeriodStart { get; set; }
        // Último día del ciclo (inclusive).
        public DateTime PeriodEnd { get; set; }
        public decimal FixedFee { get; set; }
        public decimal PricePerDocument { get; set; }
        public int? IncludedDocuments { get; set; }
        public List<PlanUsageUnit> Units { get; set; } = new();
        public int Documents => Units.Sum(u => u.Documents);
        public int Extra => Units.Sum(u => u.Extra);
        public decimal FeeAmount => Units.Sum(u => u.Fee);
        public decimal ExtraAmount => Units.Sum(u => u.ExtraAmount);
        public decimal Total => Units.Sum(u => u.Total);
    }

    // Consumo de un Client frente a su plan en un ciclo: documentos contados, incluidos, excedente y valor a cobrar. No guarda nada: se calcula al
    // consultar. Un ciclo dura 1, 3 o 12 meses y se cuenta desde el mes en que empieza la asignación del plan (el mensual es el mes calendario).
    public class ClientPlanUsageService
    {
        private readonly FelDbContext _dbContext;

        public ClientPlanUsageService(FelDbContext dbContext) => _dbContext = dbContext;

        public static int CycleMonths(PlanCycle cycle) => cycle switch { PlanCycle.Quarterly => 3, PlanCycle.Annual => 12, _ => 1 };

        // Inicio del ciclo que contiene "asOf", contando desde el mes de inicio de la asignación.
        public static DateTime PeriodStartFor(DateTime assignmentStart, PlanCycle cycle, DateTime asOf)
        {
            var anchor = new DateTime(assignmentStart.Year, assignmentStart.Month, 1);
            var length = CycleMonths(cycle);
            var monthsElapsed = (asOf.Year - anchor.Year) * 12 + (asOf.Month - anchor.Month);
            if (monthsElapsed < 0) return anchor;
            return anchor.AddMonths(monthsElapsed / length * length);
        }

        // Cuota, excedente y total de un conjunto de documentos contra las reglas del plan. Separada para poder probarla con números a mano.
        public static (int Extra, decimal ExtraAmount) Charge(int documents, int? included, decimal pricePerDocument)
        {
            if (included == null) return (0, 0m); // ilimitado: solo se cobra la cuota
            var extra = Math.Max(0, documents - included.Value);
            return (extra, extra * pricePerDocument);
        }

        public async Task<ClientPlanAssignment?> CurrentAssignmentAsync(Guid clientId, DateTime asOf)
        {
            var day = asOf.Date;
            return await _dbContext.ClientPlanAssignments.AsNoTracking()
                .Include(a => a.Plan)
                .Where(a => a.ClientId == clientId && a.StartDate <= day && (a.EndDate == null || a.EndDate >= day))
                .OrderByDescending(a => a.StartDate)
                .FirstOrDefaultAsync();
        }

        // Asigna un plan a un cliente desde "start": cierra el plan vigente el día anterior (queda el historial) y descarta asignaciones futuras. Si el
        // vigente empezó ese mismo día, se reemplaza en lugar de dejar dos el mismo día. No guarda: quien llama hace SaveChanges.
        public async Task AssignAsync(Guid clientId, Guid planId, DateTime start, decimal? feeOverride, decimal? priceOverride, PlanExceedAction? exceedOverride = null, PlanInvoicingMode invoicingMode = PlanInvoicingMode.Default)
        {
            var day = start.Date;
            var all = await _dbContext.ClientPlanAssignments.Where(a => a.ClientId == clientId).ToListAsync();

            _dbContext.ClientPlanAssignments.RemoveRange(all.Where(a => a.StartDate > day));

            var current = all.Where(a => a.StartDate <= day && (a.EndDate == null || a.EndDate >= day)).OrderByDescending(a => a.StartDate).FirstOrDefault();
            if (current != null && current.StartDate == day)
            {
                current.PlanId = planId;
                current.FixedFeeOverride = feeOverride;
                current.PricePerDocumentOverride = priceOverride;
                current.ExceedActionOverride = exceedOverride;
                current.InvoicingMode = invoicingMode;
                return;
            }
            if (current != null) current.EndDate = day.AddDays(-1);

            _dbContext.ClientPlanAssignments.Add(new ClientPlanAssignment
            {
                Id = Guid.NewGuid(), ClientId = clientId, PlanId = planId, StartDate = day,
                FixedFeeOverride = feeOverride, PricePerDocumentOverride = priceOverride, ExceedActionOverride = exceedOverride, InvoicingMode = invoicingMode, CreatedAt = DateTime.UtcNow
            });
        }

        // Quita el plan del cliente: el vigente termina el día anterior a "from".
        public async Task UnassignAsync(Guid clientId, DateTime from)
        {
            var day = from.Date;
            var all = await _dbContext.ClientPlanAssignments.Where(a => a.ClientId == clientId).ToListAsync();
            _dbContext.ClientPlanAssignments.RemoveRange(all.Where(a => a.StartDate >= day));
            foreach (var a in all.Where(a => a.StartDate < day && (a.EndDate == null || a.EndDate >= day))) a.EndDate = day.AddDays(-1);
        }

        // Consumo del ciclo que contiene "asOf". Nulo si el cliente no tiene un plan vigente ese día.
        public async Task<PlanUsage?> GetUsageAsync(Guid clientId, DateTime asOf)
        {
            var assignment = await CurrentAssignmentAsync(clientId, asOf);
            if (assignment?.Plan == null) return null;
            var plan = assignment.Plan;

            var start = PeriodStartFor(assignment.StartDate, plan.Cycle, asOf);
            var endExclusive = start.AddMonths(CycleMonths(plan.Cycle));
            var fee = assignment.FixedFeeOverride ?? plan.FixedFee;
            var price = assignment.PricePerDocumentOverride ?? plan.PricePerDocument;

            var usage = new PlanUsage
            {
                AssignmentId = assignment.Id,
                PlanId = plan.Id,
                PlanName = plan.Name,
                Cycle = plan.Cycle,
                FeeTiming = plan.FeeTiming,
                Scope = plan.Scope,
                PeriodStart = start,
                PeriodEnd = endExclusive.AddDays(-1),
                FixedFee = fee,
                PricePerDocument = price,
                IncludedDocuments = plan.IncludedDocuments
            };

            var countedByBranch = await CountDocumentsAsync(clientId, plan, start, endExclusive);

            if (plan.Scope == PlanScope.PerClient)
            {
                var documents = countedByBranch.Values.Sum();
                var (extra, extraAmount) = Charge(documents, plan.IncludedDocuments, price);
                usage.Units.Add(new PlanUsageUnit { Name = "Toda la empresa", Documents = documents, Included = plan.IncludedDocuments, Extra = extra, Fee = fee, ExtraAmount = extraAmount });
                return usage;
            }

            // Por sucursal: cada sucursal activa con su cuota y sus documentos incluidos; una sucursal inactiva solo aparece si tuvo documentos.
            var branches = await _dbContext.Branches.AsNoTracking().Where(b => b.ClientId == clientId).OrderByDescending(b => b.IsMain).ThenBy(b => b.Name).ToListAsync();
            foreach (var branch in branches)
            {
                var documents = countedByBranch.GetValueOrDefault(branch.Id);
                if (!branch.IsActive && documents == 0) continue;
                var (extra, extraAmount) = Charge(documents, plan.IncludedDocuments, price);
                usage.Units.Add(new PlanUsageUnit
                {
                    BranchId = branch.Id, Name = branch.Name, Documents = documents, Included = plan.IncludedDocuments, Extra = extra,
                    Fee = branch.IsActive ? fee : 0m, ExtraAmount = extraAmount
                });
            }
            return usage;
        }

        // Los últimos "count" ciclos del plan vigente, del más reciente al más antiguo (el de hoy incluido).
        public async Task<List<PlanUsage>> GetHistoryAsync(Guid clientId, DateTime asOf, int count)
        {
            var list = new List<PlanUsage>();
            var current = await GetUsageAsync(clientId, asOf);
            if (current == null) return list;
            list.Add(current);
            var months = CycleMonths(current.Cycle);
            var cursor = current.PeriodStart.AddMonths(-months);
            while (list.Count < count)
            {
                var past = await GetUsageAsync(clientId, cursor);
                if (past == null) break;
                list.Add(past);
                cursor = cursor.AddMonths(-months);
            }
            return list;
        }

        // Documentos del cliente en el ciclo que cuentan según el plan, por sucursal.
        private async Task<Dictionary<Guid, int>> CountDocumentsAsync(Guid clientId, TenantPlan plan, DateTime start, DateTime endExclusive)
        {
            var statuses = new List<string>();
            if (plan.CountedStatuses.HasFlag(PlanCountedStatuses.Approved)) statuses.Add("APPROVED");
            if (plan.CountedStatuses.HasFlag(PlanCountedStatuses.Rejected)) statuses.Add("REJECTED");
            if (plan.CountedStatuses.HasFlag(PlanCountedStatuses.InProcess)) { statuses.Add("PENDING"); statuses.Add("PROCESSING"); }
            if (statuses.Count == 0) return new Dictionary<Guid, int>();

            var types = plan.CountedTypes;
            var rows = await _dbContext.Documents.AsNoTracking()
                .Where(d => d.ClientId == clientId && d.CreatedAt >= start && d.CreatedAt < endExclusive && statuses.Contains(d.Status))
                .Select(d => new { d.BranchId, d.TypeCode })
                .ToListAsync();

            return rows
                .Where(d => Counts(types, d.TypeCode))
                .GroupBy(d => d.BranchId)
                .ToDictionary(g => g.Key, g => g.Count());
        }

        // TypeCode: FE* facturas, NC/ND notas, DS* documento soporte, NE* nómina.
        public static bool Counts(PlanCountedTypes types, string typeCode)
        {
            var code = (typeCode ?? string.Empty).ToUpperInvariant();
            if (code.StartsWith("FE")) return types.HasFlag(PlanCountedTypes.Invoices);
            if (code == "NC" || code == "ND") return types.HasFlag(PlanCountedTypes.Notes);
            if (code.StartsWith("DS")) return types.HasFlag(PlanCountedTypes.SupportDocuments);
            if (code.StartsWith("NE")) return types.HasFlag(PlanCountedTypes.Payroll);
            return false;
        }
    }
}
