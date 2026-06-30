using System;
using System.Linq;
using System.Threading.Tasks;
using Fel.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Fel.Api.Controllers
{
    [ApiController]
    [Route("api/superadmin/dashboard")]
    public class SuperadminDashboardController : ControllerBase
    {
        private readonly FelDbContext _dbContext;

        public SuperadminDashboardController(FelDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        [HttpGet("metrics")]
        public async Task<IActionResult> GetMetrics()
        {
            var activeTenants = await _dbContext.Tenants.CountAsync(t => t.IsActive);
            
            var today = DateTime.UtcNow;
            var startOfMonth = new DateTime(today.Year, today.Month, 1);

            // Documentos procesados este mes
            var docsThisMonth = await _dbContext.Documents
                .Where(d => d.ProcessedAt >= startOfMonth && d.Status == "APPROVED")
                .CountAsync();

            // Facturación estimada (Documentos de este mes * precio que se les aplicó)
            var estimatedBilling = await _dbContext.Documents
                .Where(d => d.ProcessedAt >= startOfMonth && d.Status == "APPROVED")
                .SumAsync(d => d.PriceCharged);

            return Ok(new
            {
                ActiveTenants = activeTenants,
                DocumentsThisMonth = docsThisMonth,
                EstimatedBilling = estimatedBilling
            });
        }
    }
}
