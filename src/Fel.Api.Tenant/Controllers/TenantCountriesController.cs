using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Fel.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Fel.Api.Tenant.Controllers
{
    [ApiController]
    [Route("api/tenant/countries")]
    public class TenantCountriesController : ControllerBase
    {
        private readonly ICoreApiClient _core;

        public TenantCountriesController(ICoreApiClient core)
        {
            _core = core;
        }

        [HttpGet]
        public async Task<IActionResult> GetAvailableCountries(CancellationToken ct)
        {
            var countries = await _core.GetPlatformCountriesAsync(ct);
            return Ok(countries);
        }

        [HttpGet("registration-data")]
        public async Task<IActionResult> GetRegistrationData(CancellationToken ct)
        {
            var data = await _core.GetRegistrationDataAsync(ct);
            return Ok(data);
        }
    }
}
