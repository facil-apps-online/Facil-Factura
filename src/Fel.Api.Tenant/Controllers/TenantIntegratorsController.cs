using System;
using System.Linq;
using System.Threading.Tasks;
using Fel.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Fel.Api.Tenant.Controllers
{
    // Catálogo de integradores de solo lectura para el portal de Tenant — lo administra Superadmin
    // (ver Fel.Api.Superadmin/Controllers/SuperadminIntegratorsController.cs), el Tenant solo lo
    // consulta para elegir proveedor de un Client o el integrador de un paquete prepago. Filtrado
    // por TenantEnabledIntegrator: un integrador externo (Dataico, etc.) solo aparece aquí si
    // Superadmin lo habilitó explícitamente para este Tenant — de lo contrario el Tenant ni se
    // entera de que existe.
    [ApiController]
    [Route("api/tenant/integrators")]
    public class TenantIntegratorsController : ControllerBase
    {
        private readonly FelDbContext _dbContext;

        public TenantIntegratorsController(FelDbContext dbContext)
        {
            _dbContext = dbContext;
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

        [HttpGet]
        public async Task<IActionResult> GetIntegrators()
        {
            var tenantId = GetCurrentTenantId();

            var enabledIds = await _dbContext.TenantEnabledIntegrators
                .Where(e => e.TenantId == tenantId)
                .Select(e => e.IntegratorId)
                .ToListAsync();

            var integrators = await _dbContext.Integrators
                .Where(i => i.IsActive && enabledIds.Contains(i.Id))
                .Select(i => new { i.Id, i.Code, i.Name })
                .ToListAsync();

            return Ok(integrators);
        }
    }
}
