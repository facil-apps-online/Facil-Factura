using System.Linq;
using System.Threading.Tasks;
using Fel.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Fel.Api.Tenant.Controllers
{
    // Catálogo de integradores de solo lectura para el portal de Tenant — lo administra Superadmin
    // (ver Fel.Api.Superadmin/Controllers/SuperadminIntegratorsController.cs), el Tenant solo lo
    // consulta para elegir proveedor de un Client o el integrador de un paquete prepago.
    [ApiController]
    [Route("api/tenant/integrators")]
    public class TenantIntegratorsController : ControllerBase
    {
        private readonly FelDbContext _dbContext;

        public TenantIntegratorsController(FelDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        [HttpGet]
        public async Task<IActionResult> GetIntegrators()
        {
            var integrators = await _dbContext.Integrators
                .Where(i => i.IsActive)
                .Select(i => new { i.Id, i.Code, i.Name })
                .ToListAsync();

            return Ok(integrators);
        }
    }
}
