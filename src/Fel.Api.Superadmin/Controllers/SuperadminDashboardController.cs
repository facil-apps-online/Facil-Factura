using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Fel.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Fel.Api.Superadmin.Controllers
{
    [ApiController]
    [Route("api/superadmin/dashboard")]
    public class SuperadminDashboardController : ControllerBase
    {
        private readonly FelDbContext _dbContext;
        private readonly Fel.Infrastructure.Services.BillingMetricsService _billingService;

        public SuperadminDashboardController(FelDbContext dbContext, Fel.Infrastructure.Services.BillingMetricsService billingService)
        {
            _dbContext = dbContext;
            _billingService = billingService;
        }

        [HttpGet("metrics")]
        public async Task<IActionResult> GetMetrics()
        {
            var activeTenants = await _dbContext.Tenants.CountAsync(t => t.IsActive);
            
            // El mes de Colombia en curso. Emitidos y rechazados de todos los tenants (también los que se cobran por usuario), con las mismas reglas que el resto
            // de los tableros: por fecha de emisión y sin lo enviado al sandbox.
            var today = Fel.Infrastructure.Services.DocumentCounting.ToColombia(DateTime.UtcNow);
            var counts = await Fel.Infrastructure.Services.DocumentCounting.CountMonthAsync(_dbContext, today.Year, today.Month);

            return Ok(new
            {
                ActiveTenants = activeTenants,
                DocumentsThisMonth = counts.Emitted,
                DocumentsRejectedThisMonth = counts.Rejected
            });
        }

        // ----- Mes en curso contra el mes anterior -----
        public record ComparisonPoint(int Day, decimal? Current, decimal? Projected, decimal? Previous);

        public class MonthlyComparison
        {
            public int Day { get; set; }
            public int DaysInMonth { get; set; }
            public decimal GeneratedToDate { get; set; }
            public decimal ProjectedLow { get; set; }
            public decimal ProjectedHigh { get; set; }
            public decimal ProjectedCentral { get; set; }
            public decimal PreviousSameDay { get; set; }
            public decimal PreviousMonthTotal { get; set; }
            public decimal? VsPreviousSameDayPercent { get; set; }
            public decimal? ProjectedVsPreviousTotalPercent { get; set; }
            public string CurrentLabel { get; set; } = string.Empty;
            public string PreviousLabel { get; set; } = string.Empty;
            public List<ComparisonPoint> Series { get; set; } = new();
        }

        private static decimal? PercentChange(decimal value, decimal reference) => reference > 0 ? Math.Round((value / reference - 1m) * 100m, 1) : null;

        // Lo causado a hoy es exacto. La proyección del cobro por usuario también (cada sucursal activa cuenta hasta fin de mes); la del
        // cobro por documento suma al ritmo diario lo que falta del mes, y se da como rango entre el ritmo de los últimos 7 días completos y
        // el del mes hasta ayer. El día de hoy se deja fuera del ritmo porque está a medias, y la proyección nunca baja de lo ya causado.
        [HttpGet("monthly-comparison")]
        public async Task<IActionResult> GetMonthlyComparison()
        {
            // Hoy y el mes en curso son los de Colombia: el cobro y los documentos se miden en su hora (ver DocumentCounting).
            var now = Fel.Infrastructure.Services.DocumentCounting.ToColombia(DateTime.UtcNow);
            var previousStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-1);
            var day = now.Day;
            var daysInMonth = DateTime.DaysInMonth(now.Year, now.Month);
            var daysInPrevious = DateTime.DaysInMonth(previousStart.Year, previousStart.Month);

            var current = await _billingService.GetSuperadminDailyAccrualAsync(now.Year, now.Month);
            var previous = await _billingService.GetSuperadminDailyAccrualAsync(previousStart.Year, previousStart.Month);

            var previousDaily = Enumerable.Range(0, daysInPrevious).Select(i => previous.DocumentBased[i] + previous.PerUser[i]).ToArray();
            var previousCumulative = new decimal[daysInPrevious];
            decimal running = 0;
            for (var i = 0; i < daysInPrevious; i++) { running += previousDaily[i]; previousCumulative[i] = running; }

            var currentCumulative = new decimal[day];
            running = 0;
            for (var i = 0; i < day; i++) { running += current.DocumentBased[i] + current.PerUser[i]; currentCumulative[i] = running; }
            var generated = currentCumulative[day - 1];

            // Ritmo diario del cobro por documento con días completos (hasta ayer). El día 1 aún no hay ninguno: se usa el del mes anterior.
            decimal rateMonth, rateRecent;
            if (day > 1)
            {
                var full = day - 1;
                rateMonth = current.DocumentBased.Take(full).Sum() / full;
                var recent = Math.Min(7, full);
                rateRecent = current.DocumentBased.Skip(full - recent).Take(recent).Sum() / recent;
            }
            else
            {
                rateMonth = rateRecent = previous.DocumentBased.Sum() / daysInPrevious;
            }

            var documentsThroughYesterday = current.DocumentBased.Take(day - 1).Sum();
            var documentsThroughToday = current.DocumentBased.Take(day).Sum();
            var perUserFullMonth = current.PerUser.Sum();
            decimal Project(decimal rate) => perUserFullMonth + Math.Max(documentsThroughToday, documentsThroughYesterday + rate * (daysInMonth - day + 1));

            var projectedLow = Math.Min(Project(rateMonth), Project(rateRecent));
            var projectedHigh = Math.Max(Project(rateMonth), Project(rateRecent));
            var projectedCentral = Math.Round((projectedLow + projectedHigh) / 2m, 2);
            var previousSameDay = previousCumulative[Math.Min(day, daysInPrevious) - 1];

            var result = new MonthlyComparison
            {
                Day = day,
                DaysInMonth = daysInMonth,
                GeneratedToDate = Math.Round(generated, 2),
                ProjectedLow = Math.Round(projectedLow, 2),
                ProjectedHigh = Math.Round(projectedHigh, 2),
                ProjectedCentral = projectedCentral,
                PreviousSameDay = Math.Round(previousSameDay, 2),
                PreviousMonthTotal = Math.Round(previousCumulative[daysInPrevious - 1], 2),
                VsPreviousSameDayPercent = PercentChange(generated, previousSameDay),
                ProjectedVsPreviousTotalPercent = PercentChange(projectedCentral, previousCumulative[daysInPrevious - 1]),
                CurrentLabel = Fel.Infrastructure.Services.MonthRange.Label(now.Year, now.Month),
                PreviousLabel = Fel.Infrastructure.Services.MonthRange.Label(previousStart.Year, previousStart.Month)
            };

            for (var d = 1; d <= Math.Max(daysInMonth, daysInPrevious); d++)
            {
                decimal? cur = d <= day ? Math.Round(currentCumulative[d - 1], 2) : null;
                // La línea proyectada nace en el último punto real (hoy) y llega a la proyección el último día del mes.
                decimal? projected = null;
                if (d == day) projected = Math.Round(generated, 2);
                else if (d > day && d <= daysInMonth) projected = Math.Round(generated + (projectedCentral - generated) * (d - day) / (daysInMonth - day), 2);
                decimal? prev = d <= daysInPrevious ? Math.Round(previousCumulative[d - 1], 2) : null;
                result.Series.Add(new ComparisonPoint(d, cur, projected, prev));
            }

            return Ok(result);
        }

        [HttpGet("billing-metrics")]
        public async Task<IActionResult> GetBillingMetrics([FromQuery] int? year, [FromQuery] int? month)
        {
            try
            {
                // El mes en curso es el de Colombia (ver DocumentCounting).
                var localNow = Fel.Infrastructure.Services.DocumentCounting.ToColombia(DateTime.UtcNow);
                var y = year ?? localNow.Year;
                var m = month ?? localNow.Month;

                var metrics = await _billingService.GetSuperadminMetricsAsync(y, m);
                return Ok(metrics);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // ----- Consumo por rango de meses -----
        // El cobro se calcula mes a mes (tarifa por volumen de cada mes, prorrateo por días en el cobro por usuario), así que un rango es la suma de
        // sus meses, cada uno calculado como siempre. Los documentos se cuentan directo (con el cobro por usuario el servicio de cobro no los trae).
        public record RangeMonth(int Year, int Month, int DocumentsEmitted, int DocumentsRejected, decimal AmountDue);

        public class RangeTenant
        {
            public Guid TenantId { get; set; }
            public string Name { get; set; } = string.Empty;
            public List<RangeMonth> Months { get; set; } = new();
            public int DocumentsEmitted { get; set; }
            public int DocumentsRejected { get; set; }
            public decimal AmountDue { get; set; }
        }

        public class RangeResult
        {
            public string From { get; set; } = string.Empty;
            public string To { get; set; } = string.Empty;
            public List<RangeMonth> Months { get; set; } = new();
            public List<RangeTenant> Tenants { get; set; } = new();
            public int TotalDocumentsEmitted { get; set; }
            public int TotalDocumentsRejected { get; set; }
            public decimal TotalAmountDue { get; set; }
        }

        private async Task<RangeResult> BuildRangeAsync(List<(int Year, int Month)> months)
        {
            // Por fecha de emisión (mes de Colombia) y sin lo enviado al sandbox: las mismas reglas del resto de los tableros (ver DocumentCounting).
            var counts = (await Fel.Infrastructure.Services.DocumentCounting.MonthlyCountsAsync(_dbContext.Documents, months[0].Year, months[0].Month, months[^1].Year, months[^1].Month))
                .GroupBy(c => new { c.TenantId, c.Year, c.Month, c.Status })
                .Select(g => new { g.Key.TenantId, g.Key.Year, g.Key.Month, g.Key.Status, Count = g.Sum(x => x.Count) })
                .ToList();

            var amounts = new Dictionary<(Guid TenantId, int Year, int Month), decimal>();
            var names = new Dictionary<Guid, string>();
            foreach (var (y, m) in months)
            {
                var monthly = await _billingService.GetSuperadminMetricsAsync(y, m);
                foreach (var t in monthly.TenantBreakdown)
                {
                    amounts[(t.TenantId, y, m)] = t.AmountDueToSuperadmin;
                    names[t.TenantId] = t.TenantName;
                }
            }

            var tenantIds = counts.Select(c => c.TenantId).Concat(names.Keys).Distinct().ToList();
            var missingNames = tenantIds.Where(id => !names.ContainsKey(id)).ToList();
            if (missingNames.Count > 0)
                foreach (var t in await _dbContext.Tenants.AsNoTracking().Where(t => missingNames.Contains(t.Id)).Select(t => new { t.Id, t.Name }).ToListAsync())
                    names[t.Id] = t.Name;

            var result = new RangeResult { From = Fel.Infrastructure.Services.MonthRange.Key(months[0].Year, months[0].Month), To = Fel.Infrastructure.Services.MonthRange.Key(months[^1].Year, months[^1].Month) };
            foreach (var id in tenantIds)
            {
                var row = new RangeTenant { TenantId = id, Name = names.GetValueOrDefault(id, string.Empty) };
                foreach (var (y, m) in months)
                {
                    row.Months.Add(new RangeMonth(y, m,
                        counts.Where(c => c.TenantId == id && c.Year == y && c.Month == m && c.Status == "APPROVED").Sum(c => c.Count),
                        counts.Where(c => c.TenantId == id && c.Year == y && c.Month == m && c.Status == "REJECTED").Sum(c => c.Count),
                        amounts.GetValueOrDefault((id, y, m))));
                }
                row.DocumentsEmitted = row.Months.Sum(x => x.DocumentsEmitted);
                row.DocumentsRejected = row.Months.Sum(x => x.DocumentsRejected);
                row.AmountDue = row.Months.Sum(x => x.AmountDue);
                result.Tenants.Add(row);
            }
            result.Tenants = result.Tenants.OrderByDescending(t => t.AmountDue).ThenBy(t => t.Name).ToList();
            for (var i = 0; i < months.Count; i++)
                result.Months.Add(new RangeMonth(months[i].Year, months[i].Month,
                    result.Tenants.Sum(t => t.Months[i].DocumentsEmitted), result.Tenants.Sum(t => t.Months[i].DocumentsRejected), result.Tenants.Sum(t => t.Months[i].AmountDue)));
            result.TotalDocumentsEmitted = result.Tenants.Sum(t => t.DocumentsEmitted);
            result.TotalDocumentsRejected = result.Tenants.Sum(t => t.DocumentsRejected);
            result.TotalAmountDue = result.Tenants.Sum(t => t.AmountDue);
            return result;
        }

        [HttpGet("billing-range")]
        public async Task<IActionResult> GetBillingRange([FromQuery] string? from, [FromQuery] string? to)
        {
            var error = Fel.Infrastructure.Services.MonthRange.Parse(from, to, out var months);
            if (error != null) return BadRequest(error);
            return Ok(await BuildRangeAsync(months));
        }

        [HttpGet("billing-range/export")]
        public async Task<IActionResult> ExportBillingRange([FromQuery] string? from, [FromQuery] string? to)
        {
            var error = Fel.Infrastructure.Services.MonthRange.Parse(from, to, out var months);
            if (error != null) return BadRequest(error);
            var r = await BuildRangeAsync(months);

            using var wb = new ClosedXML.Excel.XLWorkbook();
            var detail = wb.AddWorksheet("Por tenant y mes");
            string[] headers = { "Tenant", "Mes", "Emitidas", "Rechazadas", "A cobrar" };
            for (var i = 0; i < headers.Length; i++) detail.Cell(1, i + 1).Value = headers[i];
            detail.Row(1).Style.Font.Bold = true;
            var row = 2;
            foreach (var t in r.Tenants)
            {
                foreach (var x in t.Months)
                {
                    detail.Cell(row, 1).Value = t.Name;
                    detail.Cell(row, 2).Value = Fel.Infrastructure.Services.MonthRange.Label(x.Year, x.Month);
                    detail.Cell(row, 3).Value = x.DocumentsEmitted;
                    detail.Cell(row, 4).Value = x.DocumentsRejected;
                    detail.Cell(row, 5).Value = x.AmountDue;
                    row++;
                }
            }
            detail.Range(2, 5, row, 5).Style.NumberFormat.Format = "#,##0.00";
            detail.Columns().AdjustToContents();

            var totals = wb.AddWorksheet("Totales por tenant");
            string[] h2 = { "Tenant", "Emitidas", "Rechazadas", "A cobrar" };
            for (var i = 0; i < h2.Length; i++) totals.Cell(1, i + 1).Value = h2[i];
            totals.Row(1).Style.Font.Bold = true;
            var tr = 2;
            foreach (var t in r.Tenants)
            {
                totals.Cell(tr, 1).Value = t.Name;
                totals.Cell(tr, 2).Value = t.DocumentsEmitted;
                totals.Cell(tr, 3).Value = t.DocumentsRejected;
                totals.Cell(tr, 4).Value = t.AmountDue;
                tr++;
            }
            totals.Cell(tr, 1).Value = "Total";
            totals.Cell(tr, 2).Value = r.TotalDocumentsEmitted;
            totals.Cell(tr, 3).Value = r.TotalDocumentsRejected;
            totals.Cell(tr, 4).Value = r.TotalAmountDue;
            totals.Row(tr).Style.Font.Bold = true;
            totals.Range(2, 4, tr, 4).Style.NumberFormat.Format = "#,##0.00";
            totals.Columns().AdjustToContents();

            using var stream = new System.IO.MemoryStream();
            wb.SaveAs(stream);
            return new FileContentResult(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet") { FileDownloadName = $"consumo_{r.From}_a_{r.To}.xlsx" };
        }
    }
}
