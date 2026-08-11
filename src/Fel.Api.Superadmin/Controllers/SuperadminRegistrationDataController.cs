using System.Threading;
using System.Threading.Tasks;
using Fel.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Fel.Api.Superadmin.Controllers
{
    [ApiController]
    [Route("api/superadmin/registration-data")]
    public class SuperadminRegistrationDataController : ControllerBase
    {
        private readonly ICoreApiClient _core;

        public SuperadminRegistrationDataController(ICoreApiClient core)
        {
            _core = core;
        }

        [HttpGet]
        public async Task<IActionResult> GetRegistrationData(CancellationToken ct)
        {
            var data = await _core.GetRegistrationDataAsync(ct);
            return Ok(data);
        }
    }
}