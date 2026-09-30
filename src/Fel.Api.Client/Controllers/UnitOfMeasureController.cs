using System.Linq;
using System.Threading.Tasks;
using Fel.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Fel.Api.Client.Controllers
{
    // Lectura del catálogo global de Unidades de Medida DIAN administrado por Superadmin (ver
    // Fel.Api.Superadmin/Controllers/SuperadminUnitOfMeasureController). Información de
    // referencia, no específica de ningún cliente, por eso no exige x-client-id.
    [ApiController]
    [Route("api/client/units-of-measure")]
    public class UnitOfMeasureController : ControllerBase
    {
        private readonly FelDbContext _dbContext;

        public UnitOfMeasureController(FelDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var items = await _dbContext.UnitsOfMeasure
                .Where(u => u.IsActive)
                .OrderBy(u => u.Name)
                .Select(u => new { u.Id, u.DianCode, u.Abbreviation, u.Name, u.DisplayFormat })
                .ToListAsync();

            return Ok(items);
        }
    }
}
