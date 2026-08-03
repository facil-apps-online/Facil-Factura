using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Fel.Infrastructure.Services;

namespace Fel.Api.Client.Controllers
{
    [ApiController]
    [Route("api/v1/dashboard")]
    public class ClientDashboardController : ControllerBase
    {
        private readonly BillingMetricsService _billingService;

        public ClientDashboardController(BillingMetricsService billingService)
        {
            _billingService = billingService;
        }

        private Guid GetCurrentClientId()
        {
            if (Request.Headers.TryGetValue("x-client-id", out var clientIdStr))
            {
                if (Guid.TryParse(clientIdStr, out var clientId))
                    return clientId;
            }
            throw new UnauthorizedAccessException("x-client-id Header is missing");
        }

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
    }
}
