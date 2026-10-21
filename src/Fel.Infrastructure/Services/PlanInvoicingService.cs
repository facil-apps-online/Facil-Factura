using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Fel.Core.Entities;
using Fel.Core.Models;
using Fel.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Fel.Infrastructure.Services
{
    public class PlanInvoicingResult
    {
        public int Created { get; set; }
        public int Skipped { get; set; }
        public List<string> Messages { get; set; } = new();
    }

    // Línea de cobro de un borrador, antes de armar el documento.
    public record PlanChargeLine(string Kind, string Description, decimal Quantity, decimal UnitPrice);

    // Facturación automática de los planes del tenant (opcional): cada ciclo deja un borrador de factura por cliente en la empresa emisora del tenant, con la
    // cuota y el excedente de su plan. Nunca emite. Un renglón de PlanCharge por cliente y ciclo evita repetir el borrador, aunque se descarte.
    //
    // Qué se cobra en cada ciclo C (el que contiene "hoy"), con Pv el ciclo anterior:
    //  - La cuota de C, si el plan la cobra por adelantado y el plan rige desde el primer día de C (un primer ciclo parcial no se cobra: no hay prorrateo).
    //  - La cuota de Pv, si el plan de Pv la cobra vencida.
    //  - El excedente de Pv (documentos por encima de los incluidos), siempre vencido.
    public class PlanInvoicingService
    {
        private readonly FelDbContext _db;
        private readonly ClientPlanUsageService _usage;
        private readonly ILogger<PlanInvoicingService> _logger;

        public PlanInvoicingService(FelDbContext db, ClientPlanUsageService usage, ILogger<PlanInvoicingService> logger)
        {
            _db = db;
            _usage = usage;
            _logger = logger;
        }

        // Texto del período que cubre una línea: "noviembre 2026" o "noviembre 2026 a enero 2027".
        public static string PeriodLabel(DateTime start, int months) =>
            months <= 1
                ? MonthRange.Label(start.Year, start.Month)
                : $"{MonthRange.Label(start.Year, start.Month)} a {MonthRange.Label(start.AddMonths(months - 1).Year, start.AddMonths(months - 1).Month)}";

        // Precio base de la línea: si el producto se digitó con IVA incluido, se extrae la base (hasta 6 decimales), igual que al cargarlo en una factura.
        public static decimal BaseUnitPrice(decimal price, bool includesIva, IvaTreatment treatment, decimal ivaRate) =>
            includesIva && treatment == IvaTreatment.Gravado && ivaRate > 0 ? Math.Round(price / (1 + ivaRate / 100m), 6) : price;

        // Líneas de cobro de un ciclo. "current" es el consumo del ciclo que acaba de empezar y "previous" el del anterior (nulo si no había plan).
        public static List<PlanChargeLine> BuildLines(PlanUsage? current, bool currentStartsAtCycleStart, PlanUsage? previous, bool previousStartedAtCycleStart, int cycleMonths)
        {
            var lines = new List<PlanChargeLine>();

            if (current != null && current.FeeTiming == PlanFeeTiming.Advance && currentStartsAtCycleStart)
                AddFee(lines, current, "Cuota del plan", cycleMonths);

            if (previous != null)
            {
                if (previous.FeeTiming == PlanFeeTiming.Arrears && previousStartedAtCycleStart)
                    AddFee(lines, previous, "Cuota del plan", cycleMonths);

                if (previous.Extra > 0 && previous.PricePerDocument > 0)
                {
                    lines.Add(new PlanChargeLine("Extra",
                        $"Documentos adicionales del plan {previous.PlanName} · {PeriodLabel(previous.PeriodStart, cycleMonths)} ({previous.Extra})",
                        previous.Extra, previous.PricePerDocument));
                }
            }
            return lines;
        }

        private static void AddFee(List<PlanChargeLine> lines, PlanUsage usage, string label, int cycleMonths)
        {
            var feeUnits = usage.Units.Where(u => u.Fee > 0).ToList();
            if (feeUnits.Count == 0) return;
            var perUnit = usage.Scope == PlanScope.PerBranch ? " (por sucursal)" : string.Empty;
            lines.Add(new PlanChargeLine("Fee", $"{label} {usage.PlanName} · {PeriodLabel(usage.PeriodStart, cycleMonths)}{perUnit}", feeUnits.Count, feeUnits[0].Fee));
        }

        // Genera los borradores de los clientes del tenant. "enabledOnly" = respetar el interruptor del tenant (el proceso automático); la generación
        // manual lo ignora y también el día del mes configurado.
        public async Task<PlanInvoicingResult> GenerateForTenantAsync(Guid tenantId, DateTime today, bool automatic, CancellationToken ct = default)
        {
            var result = new PlanInvoicingResult();
            var settings = await _db.TenantInvoicingSettings.AsNoTracking().FirstOrDefaultAsync(s => s.TenantId == tenantId, ct);
            if (settings?.OwnerClientId == null) { result.Messages.Add("El tenant no tiene empresa emisora configurada."); return result; }
            if (automatic && !settings.GenerateDrafts) return result;
            if (settings.FeeProductId == null) { result.Messages.Add("Falta el producto de la cuota en la configuración."); return result; }

            var ownerId = settings.OwnerClientId.Value;
            var owner = await _db.Clients.AsNoTracking().FirstOrDefaultAsync(c => c.Id == ownerId && c.TenantId == tenantId && c.IsActive, ct);
            if (owner == null) { result.Messages.Add("La empresa emisora no existe o está inactiva."); return result; }
            var tenantNit = new string((await _db.Tenants.AsNoTracking().Where(t => t.Id == tenantId).Select(t => t.TaxId).FirstOrDefaultAsync(ct) ?? string.Empty).Where(char.IsDigit).ToArray());
            if (tenantNit.Length == 0 || new string((owner.TaxId ?? string.Empty).Where(char.IsDigit).ToArray()) != tenantNit)
            { result.Messages.Add("La empresa emisora debe tener el mismo NIT del tenant."); return result; }

            var ownerDefaults = await _db.ClientInvoiceDefaults.AsNoTracking().FirstOrDefaultAsync(d => d.ClientId == ownerId, ct);
            var mainBranchId = await BranchProvisioning.MainBranchIdAsync(_db, ownerId);
            var documentTypeId = ownerDefaults?.DocumentTypeId
                ?? await _db.DocumentTypes.AsNoTracking().Where(d => d.Code == "FE" && d.IsActive).Select(d => (Guid?)d.Id).FirstOrDefaultAsync(ct);
            if (documentTypeId == null) { result.Messages.Add("No se encontró el tipo de documento de factura de venta."); return result; }
            var documentType = await _db.DocumentTypes.AsNoTracking().FirstAsync(d => d.Id == documentTypeId.Value, ct);

            var assignments = await _db.ClientPlanAssignments.AsNoTracking()
                .Include(a => a.Plan)
                .Where(a => a.Client!.TenantId == tenantId && a.ClientId != ownerId && a.Client.IsActive && !a.Client.IsDeveloperSandbox
                            && a.InvoicingMode != PlanInvoicingMode.Skip && a.StartDate <= today && (a.EndDate == null || a.EndDate >= today))
                .ToListAsync(ct);
            if (assignments.Count == 0) return result;

            var plans = await _db.TenantPlans.AsNoTracking().Where(p => p.TenantId == tenantId).ToDictionaryAsync(p => p.Id, ct);
            var wantedProducts = plans.Values.SelectMany(p => new[] { p.FeeProductId, p.ExtraProductId })
                .Concat(new[] { settings.FeeProductId, settings.ExtraProductId }).Where(id => id != null).Select(id => id!.Value).Distinct().ToList();
            var products = await _db.Products.AsNoTracking().Include(p => p.UnitOfMeasure)
                .Where(p => p.ClientId == ownerId && wantedProducts.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);

            foreach (var assignment in assignments)
            {
                ct.ThrowIfCancellationRequested();
                var plan = assignment.Plan!;
                var cycleMonths = ClientPlanUsageService.CycleMonths(plan.Cycle);
                var cycleStart = ClientPlanUsageService.PeriodStartFor(assignment.StartDate, plan.Cycle, today);

                if (automatic && today < cycleStart.AddDays(Math.Clamp(settings.DraftDay, 1, 28) - 1)) { result.Skipped++; continue; }
                if (await _db.PlanCharges.AnyAsync(c => c.ClientId == assignment.ClientId && c.RunPeriod == cycleStart, ct)) { result.Skipped++; continue; }

                try
                {
                    var created = await GenerateForAssignmentAsync(owner, mainBranchId, documentType, ownerDefaults, settings, plans, products, assignment, cycleStart, cycleMonths, today, ct);
                    if (created) result.Created++; else result.Skipped++;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _db.ChangeTracker.Clear();
                    result.Skipped++;
                    if (result.Messages.Count < 20) result.Messages.Add($"Cliente {assignment.ClientId}: {ex.Message}");
                    _logger.LogError(ex, "No se pudo generar el cobro del plan del cliente {ClientId}.", assignment.ClientId);
                }
            }
            return result;
        }

        private async Task<bool> GenerateForAssignmentAsync(Client owner, Guid mainBranchId, DocumentType documentType, ClientInvoiceDefaults? ownerDefaults,
            TenantInvoicingSettings settings, Dictionary<Guid, TenantPlan> plans, Dictionary<Guid, Product> products, ClientPlanAssignment assignment,
            DateTime cycleStart, int cycleMonths, DateTime today, CancellationToken ct)
        {
            var current = await _usage.GetUsageAsync(assignment.ClientId, today);
            var previousStart = cycleStart.AddMonths(-cycleMonths);
            var previous = await _usage.GetUsageAsync(assignment.ClientId, previousStart);
            var previousStartedAtCycleStart = false;
            if (previous != null)
            {
                var previousAssignment = await _db.ClientPlanAssignments.AsNoTracking().FirstAsync(a => a.Id == previous.AssignmentId, ct);
                previousStartedAtCycleStart = previousAssignment.StartDate.Date <= previousStart;
            }

            var lines = BuildLines(current, assignment.StartDate.Date <= cycleStart, previous, previousStartedAtCycleStart, cycleMonths);
            var feeAmount = lines.Where(l => l.Kind == "Fee").Sum(l => l.Quantity * l.UnitPrice);
            var extraAmount = lines.Where(l => l.Kind == "Extra").Sum(l => l.Quantity * l.UnitPrice);
            var plan = plans[assignment.PlanId];
            var description = string.Join("; ", lines.Select(l => l.Description));

            var charge = new PlanCharge
            {
                Id = Guid.NewGuid(), TenantId = settings.TenantId, ClientId = assignment.ClientId, AssignmentId = assignment.Id, PlanId = plan.Id, PlanName = plan.Name,
                RunPeriod = cycleStart, FeeAmount = feeAmount, ExtraAmount = extraAmount,
                Description = description.Length > 500 ? description[..500] : description, CreatedAt = DateTime.UtcNow
            };

            // Sin nada que cobrar este ciclo: se deja el renglón para no volver a evaluarlo, sin borrador.
            if (lines.Count == 0 || feeAmount + extraAmount <= 0)
            {
                _db.PlanCharges.Add(charge);
                await _db.SaveChangesAsync(ct);
                return false;
            }

            var tenantClient = await _db.Clients.AsNoTracking().FirstAsync(c => c.Id == assignment.ClientId, ct);
            var customer = await new OwnerCustomerSyncService(_db).GetOrCreateAsync(owner.Id, tenantClient, ownerDefaults, ct);

            var document = new Document
            {
                Id = Guid.NewGuid(), ClientId = owner.Id, BranchId = mainBranchId, CustomerId = customer.Id,
                Status = "DRAFT", CreatedAt = DateTime.UtcNow, IssueDate = ColombiaTime.Now,
                DocumentTypeId = documentType.Id, TypeCode = documentType.Code,
                PaymentMeans = ownerDefaults?.PaymentMeans, PaymentMeansType = ownerDefaults?.PaymentMeansType ?? "DEBITO", PaymentTermDays = ownerDefaults?.PaymentTermDays,
                Notes = ownerDefaults?.Notes ?? string.Empty, Currency = "COP"
            };

            var line = 0;
            decimal subtotal = 0, tax = 0;
            foreach (var l in lines)
            {
                var productId = l.Kind == "Extra" ? (plan.ExtraProductId ?? settings.ExtraProductId ?? plan.FeeProductId ?? settings.FeeProductId)
                                                  : (plan.FeeProductId ?? settings.FeeProductId);
                if (productId == null || !products.TryGetValue(productId.Value, out var product))
                    throw new InvalidOperationException("Falta el producto de la empresa emisora para una línea del cobro.");

                var unitPrice = BaseUnitPrice(l.UnitPrice, product.PriceIncludesIva, product.IvaTreatment, product.IvaRate);
                var rate = product.IvaTreatment == IvaTreatment.Gravado ? product.IvaRate : 0m;
                var lineBase = l.Quantity * unitPrice;
                var lineTax = Math.Round(lineBase * rate / 100m, 2, MidpointRounding.AwayFromZero);
                subtotal += lineBase;
                tax += lineTax;

                document.Items.Add(new DocumentItem
                {
                    Id = Guid.NewGuid(), DocumentId = document.Id, ProductId = product.Id, LineNumber = line++, Code = product.Code, Name = l.Description,
                    Quantity = l.Quantity, UnitPrice = unitPrice,
                    UnitOfMeasureCode = product.UnitOfMeasure?.DianCode ?? "94", UnitOfMeasureAbbreviation = product.UnitOfMeasure?.Abbreviation ?? "EA",
                    UnitOfMeasureDisplayFormat = product.UnitOfMeasure?.DisplayFormat ?? "Combined",
                    IvaTreatment = product.IvaTreatment, TaxRate = rate, DiscountRate = 0, TaxAmount = lineTax, TotalAmount = lineBase + lineTax
                });
            }
            document.Subtotal = subtotal;
            document.TaxAmount = tax;
            document.TotalAmount = subtotal + tax;

            charge.DocumentId = document.Id;
            _db.Documents.Add(document);
            _db.PlanCharges.Add(charge);
            await _db.SaveChangesAsync(ct);
            return true;
        }

        // Para el proceso periódico: todos los tenants con el servicio activado.
        public async Task<int> RunAllAsync(DateTime today, CancellationToken ct = default)
        {
            var tenantIds = await _db.TenantInvoicingSettings.AsNoTracking().Where(s => s.GenerateDrafts && s.OwnerClientId != null).Select(s => s.TenantId).ToListAsync(ct);
            var created = 0;
            foreach (var tenantId in tenantIds)
            {
                var result = await GenerateForTenantAsync(tenantId, today, automatic: true, ct);
                created += result.Created;
            }
            return created;
        }
    }
}
