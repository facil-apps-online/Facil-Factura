using System.Linq;
using System.Threading.Tasks;
using Fel.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Fel.Api.Client.Controllers
{
    // Lectura del catálogo global de tipos de documento de identificación administrado por
    // Superadmin (ver Fel.Api.Superadmin/Controllers/SuperadminIdentificationTypesController).
    // Es información de referencia, no específica de ningún tenant/cliente, por eso no exige
    // x-client-id.
    [ApiController]
    [Route("api/client/identification-types")]
    public class IdentificationTypesController : ControllerBase
    {
        private readonly FelDbContext _dbContext;

        public IdentificationTypesController(FelDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var items = await _dbContext.IdentificationTypes
                .Where(t => t.IsActive)
                .OrderBy(t => t.Name)
                .Select(t => new { t.Id, t.Code, t.Name })
                .ToListAsync();

            return Ok(items);
        }
    }
}
