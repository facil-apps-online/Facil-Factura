using System;
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
            
            var today = DateTime.UtcNow;
            
            var metrics = await _billingService.GetSuperadminMetricsAsync(today.Year, today.Month);

            return Ok(new
            {
                ActiveTenants = activeTenants,
                DocumentsThisMonth = metrics.TotalDocuments,
                EstimatedBilling = metrics.TotalAmountDueFromTenants
            });
        }

        [HttpGet("billing-metrics")]
        public async Task<IActionResult> GetBillingMetrics([FromQuery] int? year, [FromQuery] int? month)
        {
            try
            {
                var y = year ?? DateTime.UtcNow.Year;
                var m = month ?? DateTime.UtcNow.Month;

                var metrics = await _billingService.GetSuperadminMetricsAsync(y, m);
                return Ok(metrics);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }
    }
}
