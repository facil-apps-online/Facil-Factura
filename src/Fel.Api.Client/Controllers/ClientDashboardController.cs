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
                // El mes en curso es el de Colombia (ver DocumentCounting).
                var localNow = DocumentCounting.ToColombia(DateTime.UtcNow);
                var y = year ?? localNow.Year;
                var m = month ?? localNow.Month;

                var metrics = await _billingService.GetClientMetricsAsync(clientId, y, m);
                return Ok(metrics);
            }
            catch (Exception ex)
            {
                return Unauthorized(ex.Message);
            }
        }

        // Mismas reglas que los tableros del tenant y del superadmin (ver DocumentCounting): emitido = aprobado, rechazado = rechazado (también con la emisión
        // directa), por fecha de emisión en el mes de Colombia y sin lo enviado al sandbox. Un borrador no cuenta; lo que está en validación no tiene fecha de
        // emisión todavía y se mide por la de creación.
        [HttpGet("summary")]
        public async Task<IActionResult> GetSummary()
        {
            try
            {
                var clientId = GetCurrentClientId();
                var now = DateTime.UtcNow;
                var local = DocumentCounting.ToColombia(now);
                var periodStart = new DateTime(local.Year, local.Month, 1, 0, 0, 0, DateTimeKind.Utc);
                var periodEnd = periodStart.AddMonths(1);
                var (windowStart, windowEnd) = DocumentCounting.MonthWindowUtc(local.Year, local.Month);

                var counted = DocumentCounting.Counted(_dbContext.Documents.AsNoTracking().ForBranch(CurrentBranchScope).Where(d => d.ClientId == clientId));
                var emittedDocs = await DocumentCounting.EmittedBetween(DocumentCounting.Emitted(counted), windowStart, windowEnd)
                    // TotalAmount es el bruto (subtotal + IVA): el total del documento aplica además el descuento y el cargo general.
                    .Select(d => new { d.TypeCode, TotalAmount = d.TotalAmount - (d.GeneralDiscountAmount ?? 0) - (d.RoundingAdjustmentAmount ?? 0) + (d.GeneralChargeAmount ?? 0) })
                    .ToListAsync();
                var totalRejected = await DocumentCounting.EmittedBetween(DocumentCounting.Rejected(counted), windowStart, windowEnd).CountAsync();
                var totalPending = await counted.CountAsync(d => (d.Status == "PENDING" || d.Status == "PROCESSING") && d.CreatedAt >= windowStart && d.CreatedAt < windowEnd);
                var isSandboxClient = await _dbContext.Clients.AsNoTracking().Where(c => c.Id == clientId).Select(c => c.IsDemo || c.IsDeveloperSandbox).FirstOrDefaultAsync();

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
                        TotalAmount = d.TotalAmount - (d.GeneralDiscountAmount ?? 0) - (d.RoundingAdjustmentAmount ?? 0) + (d.GeneralChargeAmount ?? 0)
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
                // Con Dataico quien firma y transmite es Dataico con su propio certificado; solo la
                // emisión directa a la DIAN (NATIVE) necesita uno cargado aquí.
                var integratorCode = await _dbContext.Clients.AsNoTracking()
                    .Where(c => c.Id == clientId)
                    .Select(c => c.Integrator.Code)
                    .FirstOrDefaultAsync();
                if (integratorCode != "DATAICO")
                {
                    var hasActiveCertificate = await _dbContext.Certificates.AnyAsync(c => c.ClientId == clientId && c.IsActive && c.ExpirationDate > now);
                    if (!hasActiveCertificate) pendingSetupItems.Add("no-certificate");
                }

                return Ok(new
                {
                    Period = new { Start = periodStart, End = periodEnd.AddDays(-1) },
                    TotalIssued = emittedDocs.Count,
                    TotalPending = totalPending,
                    TotalRejected = totalRejected,
                    // Un cliente de demostración o sandbox no cuenta lo que emite: el portal lo avisa.
                    IsSandboxClient = isSandboxClient,
                    // Las notas crédito restan del total facturado (ajustan una factura previa);
                    // el resto (facturas, notas débito) suma — mismo criterio que InvoicesPage.tsx. Solo lo emitido.
                    TotalBilled = emittedDocs.Sum(d => (d.TypeCode == "NC" ? -1 : 1) * d.TotalAmount),
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
