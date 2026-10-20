using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Fel.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Fel.Api.Tenant.Controllers
{
    [ApiController]
    [Route("api/tenant/dashboard")]
    public class TenantDashboardController : ControllerBase
    {
        private readonly FelDbContext _dbContext;
        private readonly Fel.Infrastructure.Services.BillingMetricsService _billingService;

        public TenantDashboardController(FelDbContext dbContext, Fel.Infrastructure.Services.BillingMetricsService billingService)
        {
            _dbContext = dbContext;
            _billingService = billingService;
        }

        private Guid GetCurrentTenantId()
        {
            if (Request.Headers.TryGetValue("x-tenant-id", out var tenantIdStr))
            {
                if (Guid.TryParse(tenantIdStr, out var tenantId))
                    return tenantId;
            }
            throw new UnauthorizedAccessException("x-tenant-id Header is missing");
        }

        [HttpGet("metrics")]
        public async Task<IActionResult> GetMetrics()
        {
            try
            {
                var tenantId = GetCurrentTenantId();

                var query = _dbContext.Documents
                    .Include(d => d.Client)
                    .Where(d => d.Client.TenantId == tenantId);

                var totalApproved = await query.CountAsync(d => d.Status == "APPROVED");
                // "Emitidas" son las aprobadas por la DIAN; los rechazados y los que están en proceso se cuentan aparte.
                var totalIssued = totalApproved;
                var totalRejected = await query.CountAsync(d => d.Status == "REJECTED");
                var totalProcessing = await query.CountAsync(d => d.Status == "PENDING" || d.Status == "PROCESSING");

                return Ok(new
                {
                    totalIssued,
                    totalApproved,
                    totalRejected,
                    totalProcessing
                });
            }
            catch (Exception ex)
            {
                return Unauthorized(ex.Message);
            }
        }

        // Un aliado (tenant con padre) solo ve lo que debe a la plataforma si su padre se lo activó; el tenant principal siempre lo ve.
        private Task<bool> IsPlatformBillingHiddenAsync(Guid tenantId) =>
            _dbContext.Tenants.AsNoTracking().AnyAsync(t => t.Id == tenantId && t.ParentTenantId != null && !t.CanViewPlatformBilling);

        [HttpGet("billing-metrics")]
        public async Task<IActionResult> GetBillingMetrics([FromQuery] int? year, [FromQuery] int? month)
        {
            try
            {
                return Ok(await BuildMonthAsync(GetCurrentTenantId(), year ?? DateTime.UtcNow.Year, month ?? DateTime.UtcNow.Month));
            }
            catch (Exception ex)
            {
                return Unauthorized(ex.Message);
            }
        }

        // Un mes de cobro del tenant, ya con los emitidos y rechazados por cliente y con lo que su padre no le muestra oculto. Lo usan el tablero
        // (un mes) y los rangos por meses.
        private async Task<Fel.Infrastructure.Services.TenantBillingMetrics> BuildMonthAsync(Guid tenantId, int y, int m)
        {
            {
                var metrics = await _billingService.GetTenantMetricsAsync(tenantId, y, m);

                // La lista de consumo trae los documentos aprobados (lo que se cobra); aquí se le suman los rechazados del mes por cliente y sucursal.
                var periodStart = new DateTime(y, m, 1, 0, 0, 0, DateTimeKind.Utc);
                var periodEnd = periodStart.AddMonths(1);
                // Los emitidos (aprobados) también se cuentan aquí directamente: con el cobro por usuario el servicio de cobro no los trae (los deja en 0),
                // y con el cobro por documento da lo mismo.
                var counts = await _dbContext.Documents.AsNoTracking()
                    .Where(d => d.Client.TenantId == tenantId && !d.Client.IsDeveloperSandbox && (d.Status == "REJECTED" || d.Status == "APPROVED")
                        && d.CreatedAt >= periodStart && d.CreatedAt < periodEnd)
                    .GroupBy(d => new { d.ClientId, d.BranchId, d.Status })
                    .Select(g => new { g.Key.ClientId, g.Key.BranchId, g.Key.Status, Count = g.Count() })
                    .ToListAsync();
                foreach (var r in counts)
                {
                    var row = metrics.ClientBreakdown.FirstOrDefault(x => x.ClientId == r.ClientId && x.BranchId == r.BranchId);
                    if (row == null)
                    {
                        // Cliente (o sucursal) con rechazados pero sin aprobados ni cuota: no tenía fila en el cobro.
                        var name = await _dbContext.Clients.AsNoTracking().Where(c => c.Id == r.ClientId)
                            .Select(c => !string.IsNullOrEmpty(c.CommercialName) ? c.CommercialName : c.CompanyName).FirstAsync();
                        var branchCount = await _dbContext.Branches.AsNoTracking().CountAsync(b => b.ClientId == r.ClientId);
                        var branchName = await _dbContext.Branches.AsNoTracking().Where(b => b.Id == r.BranchId).Select(b => b.Name).FirstOrDefaultAsync();
                        row = new Fel.Infrastructure.Services.ClientUsageBreakdown
                        {
                            ClientId = r.ClientId,
                            ClientName = branchCount > 1 && branchName != null ? $"{name} – {branchName}" : name,
                            BranchId = r.BranchId,
                            BranchName = branchName
                        };
                        metrics.ClientBreakdown.Add(row);
                    }
                    if (r.Status == "APPROVED") row.DocumentsEmitted = r.Count;
                    else row.RejectedDocuments = r.Count;
                }
                metrics.ClientBreakdown = metrics.ClientBreakdown.OrderByDescending(x => x.DocumentsEmitted).ThenByDescending(x => x.RejectedDocuments).ToList();

                if (await IsPlatformBillingHiddenAsync(tenantId))
                {
                    // Lo oculta el servidor, no solo la pantalla: un aliado sin permiso no recibe lo que debe a la plataforma.
                    metrics.AmountDueToSuperadmin = 0;
                    metrics.SuperadminTariffApplied = 0;
                    metrics.MonthFractionNeeded = 0;
                    metrics.PlatformBillingHidden = true;
                    // En la lista de consumo por cliente se conservan el cliente y los documentos emitidos; los valores en dinero (tarifa y subtotal)
                    // siguen la misma configuración.
                    foreach (var row in metrics.ClientBreakdown)
                    {
                        row.PriceApplied = 0;
                        row.AmountDueToTenant = 0;
                    }
                }
                return metrics;
            }
        }

        // ----- Consumo por rango de meses -----
        // El cobro se calcula mes a mes (tarifa por volumen de cada mes, prorrateo por días en el cobro por usuario), así que un rango es la suma
        // de sus meses, cada uno calculado como siempre (ver MonthRange).
        public record RangeMonthRow(int Year, int Month, int DocumentsEmitted, int DocumentsRejected, decimal AmountDueToPlatform, decimal AmountDueFromClients, string? CutStatus = null);

        public class RangeClientRow
        {
            public string Name { get; set; } = string.Empty;
            public int DocumentsEmitted { get; set; }
            public int DocumentsRejected { get; set; }
            public decimal AmountDueToTenant { get; set; }
        }

        public class BillingRangeResult
        {
            public string From { get; set; } = string.Empty;
            public string To { get; set; } = string.Empty;
            public bool PlatformBillingHidden { get; set; }
            public List<RangeMonthRow> Months { get; set; } = new();
            public int TotalDocumentsEmitted { get; set; }
            public int TotalDocumentsRejected { get; set; }
            public decimal TotalAmountDueToPlatform { get; set; }
            public decimal TotalAmountDueFromClients { get; set; }
            public List<RangeClientRow> Clients { get; set; } = new();
        }

        private async Task<BillingRangeResult> BuildRangeAsync(Guid tenantId, List<(int Year, int Month)> months)
        {
            var result = new BillingRangeResult { From = Fel.Infrastructure.Services.MonthRange.Key(months[0].Year, months[0].Month), To = Fel.Infrastructure.Services.MonthRange.Key(months[^1].Year, months[^1].Month) };
            var clients = new Dictionary<string, RangeClientRow>();
            foreach (var (y, m) in months)
            {
                var mm = await BuildMonthAsync(tenantId, y, m);
                result.PlatformBillingHidden |= mm.PlatformBillingHidden;
                result.Months.Add(new RangeMonthRow(y, m, mm.ClientBreakdown.Sum(c => c.DocumentsEmitted), mm.ClientBreakdown.Sum(c => c.RejectedDocuments), mm.AmountDueToSuperadmin, mm.AmountDueFromClients));
                foreach (var c in mm.ClientBreakdown)
                {
                    if (!clients.TryGetValue(c.ClientName, out var row)) clients[c.ClientName] = row = new RangeClientRow { Name = c.ClientName };
                    row.DocumentsEmitted += c.DocumentsEmitted;
                    row.DocumentsRejected += c.RejectedDocuments;
                    row.AmountDueToTenant += c.AmountDueToTenant;
                }
            }
            result.TotalDocumentsEmitted = result.Months.Sum(x => x.DocumentsEmitted);
            result.TotalDocumentsRejected = result.Months.Sum(x => x.DocumentsRejected);
            result.TotalAmountDueToPlatform = result.Months.Sum(x => x.AmountDueToPlatform);
            result.TotalAmountDueFromClients = result.Months.Sum(x => x.AmountDueFromClients);
            result.Clients = clients.Values.OrderByDescending(c => c.DocumentsEmitted).ThenBy(c => c.Name).ToList();
            return result;
        }

        private static IActionResult XlsxFile(ClosedXML.Excel.XLWorkbook workbook, string fileName)
        {
            using var stream = new System.IO.MemoryStream();
            workbook.SaveAs(stream);
            return new FileContentResult(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet") { FileDownloadName = fileName };
        }

        [HttpGet("billing-range")]
        public async Task<IActionResult> GetBillingRange([FromQuery] string? from, [FromQuery] string? to)
        {
            try
            {
                var error = Fel.Infrastructure.Services.MonthRange.Parse(from, to, out var months);
                if (error != null) return BadRequest(error);
                return Ok(await BuildRangeAsync(GetCurrentTenantId(), months));
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(ex.Message);
            }
        }

        [HttpGet("billing-range/export")]
        public async Task<IActionResult> ExportBillingRange([FromQuery] string? from, [FromQuery] string? to)
        {
            try
            {
                var error = Fel.Infrastructure.Services.MonthRange.Parse(from, to, out var months);
                if (error != null) return BadRequest(error);
                var r = await BuildRangeAsync(GetCurrentTenantId(), months);

                using var wb = new ClosedXML.Excel.XLWorkbook();
                var byMonth = wb.AddWorksheet("Por mes");
                string[] headers = r.PlatformBillingHidden
                    ? new[] { "Mes", "Emitidas", "Rechazadas" }
                    : new[] { "Mes", "Emitidas", "Rechazadas", "A pagar a la plataforma", "A cobrar a clientes" };
                for (var i = 0; i < headers.Length; i++) byMonth.Cell(1, i + 1).Value = headers[i];
                var row = 2;
                foreach (var x in r.Months)
                {
                    byMonth.Cell(row, 1).Value = Fel.Infrastructure.Services.MonthRange.Label(x.Year, x.Month);
                    byMonth.Cell(row, 2).Value = x.DocumentsEmitted;
                    byMonth.Cell(row, 3).Value = x.DocumentsRejected;
                    if (!r.PlatformBillingHidden) { byMonth.Cell(row, 4).Value = x.AmountDueToPlatform; byMonth.Cell(row, 5).Value = x.AmountDueFromClients; }
                    row++;
                }
                byMonth.Cell(row, 1).Value = "Total";
                byMonth.Cell(row, 2).Value = r.TotalDocumentsEmitted;
                byMonth.Cell(row, 3).Value = r.TotalDocumentsRejected;
                if (!r.PlatformBillingHidden) { byMonth.Cell(row, 4).Value = r.TotalAmountDueToPlatform; byMonth.Cell(row, 5).Value = r.TotalAmountDueFromClients; }
                byMonth.Row(row).Style.Font.Bold = true;
                byMonth.Row(1).Style.Font.Bold = true;
                byMonth.Range(2, 4, row, 5).Style.NumberFormat.Format = "#,##0.00";

                var byClient = wb.AddWorksheet("Por cliente");
                string[] clientHeaders = r.PlatformBillingHidden ? new[] { "Cliente", "Emitidas", "Rechazadas" } : new[] { "Cliente", "Emitidas", "Rechazadas", "Valor a cobrar" };
                for (var i = 0; i < clientHeaders.Length; i++) byClient.Cell(1, i + 1).Value = clientHeaders[i];
                byClient.Row(1).Style.Font.Bold = true;
                var cr = 2;
                foreach (var c in r.Clients)
                {
                    byClient.Cell(cr, 1).Value = c.Name;
                    byClient.Cell(cr, 2).Value = c.DocumentsEmitted;
                    byClient.Cell(cr, 3).Value = c.DocumentsRejected;
                    if (!r.PlatformBillingHidden) { byClient.Cell(cr, 4).Value = c.AmountDueToTenant; byClient.Cell(cr, 4).Style.NumberFormat.Format = "#,##0.00"; }
                    cr++;
                }
                byMonth.Columns().AdjustToContents();
                byClient.Columns().AdjustToContents();
                return XlsxFile(wb, $"consumo_{r.From}_a_{r.To}.xlsx");
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(ex.Message);
            }
        }

        // Consumo del grupo (este tenant y sus aliados directos) por rango de meses, mes a mes y calculado en vivo, incluido el mes en curso.
        public class GroupRangeTenantRow
        {
            public Guid TenantId { get; set; }
            public string Name { get; set; } = string.Empty;
            public bool IsSelf { get; set; }
            public bool PlatformBillingHidden { get; set; }
            public List<RangeMonthRow> Months { get; set; } = new();
            public int DocumentsEmitted { get; set; }
            public int DocumentsRejected { get; set; }
            public decimal AmountDueToPlatform { get; set; }
        }

        public class GroupRangeResult
        {
            public string From { get; set; } = string.Empty;
            public string To { get; set; } = string.Empty;
            public bool IsGroup { get; set; }
            public List<RangeMonthRow> Months { get; set; } = new();
            public List<GroupRangeTenantRow> Tenants { get; set; } = new();
            public int TotalDocumentsEmitted { get; set; }
            public int TotalDocumentsRejected { get; set; }
            public decimal TotalAmountDueToPlatform { get; set; }
        }

        private async Task<GroupRangeResult> BuildGroupRangeAsync(Guid tenantId, List<(int Year, int Month)> months)
        {
            var tenants = await _dbContext.Tenants.AsNoTracking()
                .Where(t => t.Id == tenantId || t.ParentTenantId == tenantId)
                .Select(t => new { t.Id, Name = !string.IsNullOrEmpty(t.CommercialName) ? t.CommercialName : t.Name })
                .ToListAsync();
            var selfHidden = await IsPlatformBillingHiddenAsync(tenantId);

            var rangeStart = new DateTime(months[0].Year, months[0].Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var rangeEnd = new DateTime(months[^1].Year, months[^1].Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(1);
            var groupIds = tenants.Select(t => t.Id).ToList();
            var counts = await _dbContext.Documents.AsNoTracking()
                .Where(d => groupIds.Contains(d.Client.TenantId) && !d.Client.IsDeveloperSandbox && (d.Status == "APPROVED" || d.Status == "REJECTED")
                    && d.CreatedAt >= rangeStart && d.CreatedAt < rangeEnd)
                .GroupBy(d => new { d.Client.TenantId, d.CreatedAt.Year, d.CreatedAt.Month, d.Status })
                .Select(g => new { g.Key.TenantId, g.Key.Year, g.Key.Month, g.Key.Status, Count = g.Count() })
                .ToListAsync();

            // Meses con corte ya generado: se muestra lo que realmente se facturó (y si está pagado), no el cálculo en vivo.
            var startKey = months[0].Year * 100 + months[0].Month;
            var endKey = months[^1].Year * 100 + months[^1].Month;
            var cuts = (await _dbContext.TenantBillings.AsNoTracking()
                    .Where(b => groupIds.Contains(b.TenantId) && b.Year * 100 + b.Month >= startKey && b.Year * 100 + b.Month <= endKey)
                    .Select(b => new { b.TenantId, b.Year, b.Month, b.TotalAmount, b.Status })
                    .ToListAsync())
                .ToDictionary(b => (b.TenantId, b.Year, b.Month));

            var result = new GroupRangeResult
            {
                From = Fel.Infrastructure.Services.MonthRange.Key(months[0].Year, months[0].Month),
                To = Fel.Infrastructure.Services.MonthRange.Key(months[^1].Year, months[^1].Month),
                IsGroup = tenants.Count > 1
            };
            foreach (var t in tenants.OrderByDescending(t => t.Id == tenantId).ThenBy(t => t.Name))
            {
                var row = new GroupRangeTenantRow { TenantId = t.Id, Name = t.Name, IsSelf = t.Id == tenantId, PlatformBillingHidden = t.Id == tenantId && selfHidden };
                foreach (var (y, m) in months)
                {
                    var emitted = counts.Where(c => c.TenantId == t.Id && c.Year == y && c.Month == m && c.Status == "APPROVED").Sum(c => c.Count);
                    var rejected = counts.Where(c => c.TenantId == t.Id && c.Year == y && c.Month == m && c.Status == "REJECTED").Sum(c => c.Count);
                    cuts.TryGetValue((t.Id, y, m), out var cut);
                    var amount = row.PlatformBillingHidden ? 0m : cut != null ? cut.TotalAmount : (await _billingService.GetTenantMetricsAsync(t.Id, y, m)).AmountDueToSuperadmin;
                    row.Months.Add(new RangeMonthRow(y, m, emitted, rejected, amount, 0m, row.PlatformBillingHidden ? null : cut?.Status.ToString()));
                }
                row.DocumentsEmitted = row.Months.Sum(x => x.DocumentsEmitted);
                row.DocumentsRejected = row.Months.Sum(x => x.DocumentsRejected);
                row.AmountDueToPlatform = row.Months.Sum(x => x.AmountDueToPlatform);
                result.Tenants.Add(row);
            }
            for (var i = 0; i < months.Count; i++)
                result.Months.Add(new RangeMonthRow(months[i].Year, months[i].Month,
                    result.Tenants.Sum(t => t.Months[i].DocumentsEmitted), result.Tenants.Sum(t => t.Months[i].DocumentsRejected),
                    result.Tenants.Sum(t => t.Months[i].AmountDueToPlatform), 0m));
            result.TotalDocumentsEmitted = result.Tenants.Sum(t => t.DocumentsEmitted);
            result.TotalDocumentsRejected = result.Tenants.Sum(t => t.DocumentsRejected);
            result.TotalAmountDueToPlatform = result.Tenants.Sum(t => t.AmountDueToPlatform);
            return result;
        }

        [HttpGet("group-billing-range")]
        public async Task<IActionResult> GetGroupBillingRange([FromQuery] string? from, [FromQuery] string? to)
        {
            try
            {
                var error = Fel.Infrastructure.Services.MonthRange.Parse(from, to, out var months);
                if (error != null) return BadRequest(error);
                return Ok(await BuildGroupRangeAsync(GetCurrentTenantId(), months));
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(ex.Message);
            }
        }

        [HttpGet("group-billing-range/export")]
        public async Task<IActionResult> ExportGroupBillingRange([FromQuery] string? from, [FromQuery] string? to)
        {
            try
            {
                var error = Fel.Infrastructure.Services.MonthRange.Parse(from, to, out var months);
                if (error != null) return BadRequest(error);
                var r = await BuildGroupRangeAsync(GetCurrentTenantId(), months);

                using var wb = new ClosedXML.Excel.XLWorkbook();
                var ws = wb.AddWorksheet("Grupo por mes");
                string[] headers = { "Tenant", "Mes", "Emitidas", "Rechazadas", "A pagar a la plataforma" };
                for (var i = 0; i < headers.Length; i++) ws.Cell(1, i + 1).Value = headers[i];
                ws.Row(1).Style.Font.Bold = true;
                var row = 2;
                foreach (var t in r.Tenants)
                {
                    foreach (var x in t.Months)
                    {
                        ws.Cell(row, 1).Value = t.Name;
                        ws.Cell(row, 2).Value = Fel.Infrastructure.Services.MonthRange.Label(x.Year, x.Month);
                        ws.Cell(row, 3).Value = x.DocumentsEmitted;
                        ws.Cell(row, 4).Value = x.DocumentsRejected;
                        if (!t.PlatformBillingHidden) ws.Cell(row, 5).Value = x.AmountDueToPlatform;
                        row++;
                    }
                }
                ws.Range(2, 5, row, 5).Style.NumberFormat.Format = "#,##0.00";
                ws.Columns().AdjustToContents();

                var totals = wb.AddWorksheet("Totales por tenant");
                string[] h2 = { "Tenant", "Emitidas", "Rechazadas", "A pagar a la plataforma" };
                for (var i = 0; i < h2.Length; i++) totals.Cell(1, i + 1).Value = h2[i];
                totals.Row(1).Style.Font.Bold = true;
                var tr = 2;
                foreach (var t in r.Tenants)
                {
                    totals.Cell(tr, 1).Value = t.Name;
                    totals.Cell(tr, 2).Value = t.DocumentsEmitted;
                    totals.Cell(tr, 3).Value = t.DocumentsRejected;
                    if (!t.PlatformBillingHidden) totals.Cell(tr, 4).Value = t.AmountDueToPlatform;
                    tr++;
                }
                totals.Cell(tr, 1).Value = "Total del grupo";
                totals.Cell(tr, 2).Value = r.TotalDocumentsEmitted;
                totals.Cell(tr, 3).Value = r.TotalDocumentsRejected;
                totals.Cell(tr, 4).Value = r.TotalAmountDueToPlatform;
                totals.Row(tr).Style.Font.Bold = true;
                totals.Range(2, 4, tr, 4).Style.NumberFormat.Format = "#,##0.00";
                totals.Columns().AdjustToContents();
                return XlsxFile(wb, $"consumo_grupo_{r.From}_a_{r.To}.xlsx");
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(ex.Message);
            }
        }

        // Grupo empresarial: si este tenant es "padre" de otros (ver Tenant.ParentTenantId,
        // asignado desde Superadmin), consolida lo emitido a él mismo + a cada tenant asociado.
        // Se basa en TenantBilling (el corte mensual ya generado por MonthlyBillingCutService),
        // no en métricas en vivo — es lo que realmente se le facturó, independiente del modo de
        // facturación (PerDocument/PerUser) de cada tenant del grupo.
        [HttpGet("group-billing")]
        public async Task<IActionResult> GetGroupBilling([FromQuery] int? year)
        {
            try
            {
                var tenantId = GetCurrentTenantId();

                var groupTenants = await _dbContext.Tenants
                    .Where(t => t.Id == tenantId || t.ParentTenantId == tenantId)
                    .Select(t => new { t.Id, t.Name, t.CommercialName })
                    .ToListAsync();

                // Sin permiso para ver su cobro, el propio tenant sale del consolidado (sus aliados, si los tiene, siguen).
                if (await IsPlatformBillingHiddenAsync(tenantId)) groupTenants = groupTenants.Where(t => t.Id != tenantId).ToList();

                var groupIds = groupTenants.Select(t => t.Id).ToList();

                var billingsQuery = _dbContext.TenantBillings.Where(b => groupIds.Contains(b.TenantId));
                if (year.HasValue) billingsQuery = billingsQuery.Where(b => b.Year == year.Value);

                var billings = await billingsQuery
                    .OrderByDescending(b => b.Year).ThenByDescending(b => b.Month)
                    .ToListAsync();

                var byTenant = groupTenants.Select(t => new
                {
                    tenantId = t.Id,
                    tenantName = string.IsNullOrWhiteSpace(t.CommercialName) ? t.Name : t.CommercialName,
                    isSelf = t.Id == tenantId,
                    totalAmount = billings.Where(b => b.TenantId == t.Id).Sum(b => b.TotalAmount),
                    billings = billings.Where(b => b.TenantId == t.Id)
                        .Select(b => new { b.Year, b.Month, b.TotalAmount, b.Currency, b.Status, b.PaidAt })
                        .ToList()
                }).ToList();

                return Ok(new
                {
                    isGroup = groupTenants.Count > 1 || groupTenants.Any(t => t.Id != tenantId),
                    totalAmount = billings.Sum(b => b.TotalAmount),
                    currency = "COP",
                    byTenant
                });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(ex.Message);
            }
        }

        [HttpGet("recent-documents")]
        public async Task<IActionResult> GetRecentDocuments()
        {
            try
            {
                var tenantId = GetCurrentTenantId();

                var recentDocs = await _dbContext.Documents
                    .Include(d => d.Client)
                    .Where(d => d.Client.TenantId == tenantId)
                    .OrderByDescending(d => d.CreatedAt)
                    .Take(10)
                    .Select(d => new
                    {
                        id = !string.IsNullOrEmpty(d.Number) ? d.Number : d.TrackingId,
                        date = d.CreatedAt.ToString("yyyy-MM-dd HH:mm"),
                        client = d.Client.CompanyName,
                        total = "N/A", // La entidad Document guarda metadatos, no el monto total de la factura
                        status = d.Status
                    })
                    .ToListAsync();

                return Ok(recentDocs);
            }
            catch (Exception ex)
            {
                return Unauthorized(ex.Message);
            }
        }
    }
}
