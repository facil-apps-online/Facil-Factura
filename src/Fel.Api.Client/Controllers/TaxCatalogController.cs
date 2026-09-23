using System.Linq;
using System.Threading.Tasks;
using Fel.Core.Entities;
using Fel.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Fel.Api.Client.Controllers
{
    // Lectura del catálogo global de impuestos/retenciones de Dataico administrado por
    // Superadmin (ver Fel.Api.Superadmin/Controllers/SuperadminTaxCatalogController). Es
    // información de referencia, no específica de ningún tenant/cliente, por eso no exige
    // x-client-id.
    [ApiController]
    [Route("api/client/tax-catalog")]
    public class TaxCatalogController : ControllerBase
    {
        private readonly FelDbContext _dbContext;

        public TaxCatalogController(FelDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] string? kind)
        {
            var query = _dbContext.DataicoTaxCatalogItems.Where(i => i.IsActive);

            if (!string.IsNullOrEmpty(kind) && System.Enum.TryParse<TaxCatalogKind>(kind, true, out var parsedKind))
            {
                query = query.Where(i => i.Kind == parsedKind);
            }

            var items = await query
                .OrderBy(i => i.Category).ThenBy(i => i.Rate)
                .Select(i => new { i.Id, i.Category, i.Name, i.Rate })
                .ToListAsync();

            return Ok(items);
        }
    }
}
