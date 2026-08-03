using System;
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

                var totalIssued = await query.CountAsync();
                var totalApproved = await query.CountAsync(d => d.Status == "APPROVED");
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

        [HttpGet("billing-metrics")]
        public async Task<IActionResult> GetBillingMetrics([FromQuery] int? year, [FromQuery] int? month)
        {
            try
            {
                var tenantId = GetCurrentTenantId();
                var y = year ?? DateTime.UtcNow.Year;
                var m = month ?? DateTime.UtcNow.Month;

                var metrics = await _billingService.GetTenantMetricsAsync(tenantId, y, m);
                return Ok(metrics);
            }
            catch (Exception ex)
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
