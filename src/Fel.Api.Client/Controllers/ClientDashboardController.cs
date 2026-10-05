using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Fel.Core.Entities;
using Fel.Infrastructure.Data;
using Fel.Infrastructure.Services;
using Fel.Api.Security;

namespace Fel.Api.Client.Controllers
{
    [ApiController]
    [Route("api/v1/dashboard")]
    public class ClientDashboardController : ClientPortalControllerBase
    {
        private readonly BillingMetricsService _billingService;
        private readonly FelDbContext _dbContext;

        public ClientDashboardController(BillingMetricsService billingService, FelDbContext dbContext)
        {
            _billingService = billingService;
            _dbContext = dbContext;
        }

        [ClientRole(ClientUserRoles.Administrator)]
        [HttpGet("metrics")]
        public async Task<IActionResult> GetBillingMetrics([FromQuery] int? year, [FromQuery] int? month)
        {
            try
            {
                var clientId = GetCurrentClientId();
                var y = year ?? DateTime.UtcNow.Year;
                var m = month ?? DateTime.UtcNow.Month;

                var metrics = await _billingService.GetClientMetricsAsync(clientId, y, m);
                return Ok(metrics);
            }
            catch (Exception ex)
            {
                return Unauthorized(ex.Message);
            }
        }

        // Ancla el período en IssueDate (siempre lo fijamos nosotros, igual en ambos integradores) en
        // vez de ProcessedAt/fecha de confirmación — evita que la agrupación mensual dependa de cuándo
        // llega la respuesta de cada integrador (nativo: síncrono y bajo nuestro control; Dataico:
        // también síncrono en el envío, pero no queremos atar el período a ese detalle de integración).
        [HttpGet("summary")]
        public async Task<IActionResult> GetSummary()
        {
            try
            {
                var clientId = GetCurrentClientId();
                var now = DateTime.UtcNow;
                var periodStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
                var periodEnd = periodStart.AddMonths(1);

                var periodDocs = await _dbContext.Documents.AsNoTracking().ForBranch(CurrentBranchScope)
                    .Where(d => d.ClientId == clientId && d.IssueDate >= periodStart && d.IssueDate < periodEnd)
                    // TotalAmount es el bruto (subtotal + IVA): el total del documento aplica además el descuento y el cargo general.
                    .Select(d => new { d.Status, d.TypeCode, TotalAmount = d.TotalAmount - (d.GeneralDiscountAmount ?? 0) + (d.GeneralChargeAmount ?? 0) })
                    .ToListAsync();

                var recentDocuments = await _dbContext.Documents.AsNoTracking().ForBranch(CurrentBranchScope)
                    .Where(d => d.ClientId == clientId && d.Status == "APPROVED")
                    .OrderByDescending(d => d.ProcessedAt)
                    .Take(10)
                    .Select(d => new
                    {
                        d.Id,
                        d.TypeCode,
                        d.Number,
                        d.ProcessedAt,
                        CustomerName = d.Customer != null ? d.Customer.Name : null,
                        TotalAmount = d.TotalAmount - (d.GeneralDiscountAmount ?? 0) + (d.GeneralChargeAmount ?? 0)
                    })
                    .ToListAsync();

                var activeBag = await _dbContext.ClientPrepaidBags.AsNoTracking()
                    .Where(b => b.ClientId == clientId && b.Status == PrepaidBagStatus.Active && b.RemainingBalance > 0)
                    .OrderBy(b => b.PurchasedAt)
                    .FirstOrDefaultAsync();

                object consumption;
                if (activeBag != null)
                {
                    consumption = new
                    {
                        Mode = "PrepaidBag",
                        BagStartDate = activeBag.PurchasedAt,
                        AmountPaid = activeBag.AmountPaid,
                        RemainingBalance = activeBag.RemainingBalance,
                        DiscountedPricePerDocument = activeBag.DiscountedPricePerDocument
                    };
                }
                else
                {
                    var priceBranchId = CurrentBranchScope ?? await BranchProvisioning.MainBranchIdAsync(_dbContext, clientId);
                    consumption = new { Mode = "Standard", PricePerDocument = await BranchProvisioning.PricePerDocumentAsync(_dbContext, priceBranchId) };
                }

                var pendingSetupItems = new List<string>();
                var hasActiveResolution = await _dbContext.Resolutions.ForBranch(_dbContext, CurrentBranchScope).AnyAsync(r => r.ClientId == clientId && r.IsActive);
                if (!hasActiveResolution) pendingSetupItems.Add("no-resolution");
                var hasActiveCertificate = await _dbContext.Certificates.AnyAsync(c => c.ClientId == clientId && c.IsActive && c.ExpirationDate > now);
                if (!hasActiveCertificate) pendingSetupItems.Add("no-certificate");

                return Ok(new
                {
                    Period = new { Start = periodStart, End = periodEnd.AddDays(-1) },
                    TotalIssued = periodDocs.Count,
                    TotalAccepted = periodDocs.Count(d => d.Status == "APPROVED"),
                    TotalPending = periodDocs.Count(d => d.Status == "PENDING" || d.Status == "PROCESSING"),
                    TotalRejected = periodDocs.Count(d => d.Status == "REJECTED"),
                    // Las notas crédito restan del total facturado (ajustan una factura previa);
                    // el resto (facturas, notas débito) suma — mismo criterio que InvoicesPage.tsx.
                    TotalBilled = periodDocs.Sum(d => (d.TypeCode == "NC" ? -1 : 1) * d.TotalAmount),
                    RecentDocuments = recentDocuments,
                    Consumption = Branch.IsAdministrator ? consumption : null,
                    PendingSetupItems = pendingSetupItems
                });
            }
            catch (Exception ex)
            {
                return Unauthorized(ex.Message);
            }
        }
    }
}
