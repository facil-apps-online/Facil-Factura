using System;
using System.Linq;
using System.Threading.Tasks;
using Fel.Core.Entities;
using Fel.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Fel.Api.Superadmin.Controllers
{
    // Allowlist de qué integradores puede ver/usar cada Tenant (ver TenantEnabledIntegrator). Sin
    // fila aquí, el Tenant no ve ese integrador en su portal — separado a propósito de
    // SuperadminBillingController/integrator-billing, que solo define cómo se le cobra, no si
    // tiene acceso.
    [ApiController]
    [Route("api/superadmin/tenants/{tenantId}/integrators")]
    public class SuperadminTenantIntegratorsController : ControllerBase
    {
        private readonly FelDbContext _dbContext;

        public SuperadminTenantIntegratorsController(FelDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        [HttpGet]
        public async Task<IActionResult> GetTenantIntegrators(Guid tenantId)
        {
            if (!await _dbContext.Tenants.AnyAsync(t => t.Id == tenantId)) return NotFound("Tenant no existe.");

            var enabledIds = await _dbContext.TenantEnabledIntegrators
                .Where(e => e.TenantId == tenantId)
                .Select(e => e.IntegratorId)
                .ToListAsync();

            var integrators = await _dbContext.Integrators
                .Where(i => i.IsActive)
                .OrderBy(i => i.Name)
                .Select(i => new { i.Id, i.Code, i.Name, i.Kind })
                .ToListAsync();

            var result = integrators.Select(i => new
            {
                i.Id,
                i.Code,
                i.Name,
                i.Kind,
                IsEnabled = enabledIds.Contains(i.Id)
            });

            return Ok(result);
        }

        public class SetTenantIntegratorEnabledRequest
        {
            public bool Enabled { get; set; }
        }

        [HttpPut("{integratorId}")]
        public async Task<IActionResult> SetTenantIntegratorEnabled(Guid tenantId, Guid integratorId, [FromBody] SetTenantIntegratorEnabledRequest request)
        {
            if (!await _dbContext.Tenants.AnyAsync(t => t.Id == tenantId)) return NotFound("Tenant no existe.");
            if (!await _dbContext.Integrators.AnyAsync(i => i.Id == integratorId)) return NotFound("Integrador no existe.");

            var row = await _dbContext.TenantEnabledIntegrators
                .FirstOrDefaultAsync(e => e.TenantId == tenantId && e.IntegratorId == integratorId);

            if (request.Enabled)
            {
                if (row == null)
                {
                    _dbContext.TenantEnabledIntegrators.Add(new TenantEnabledIntegrator
                    {
                        Id = Guid.NewGuid(),
                        TenantId = tenantId,
                        IntegratorId = integratorId
                    });
                }
            }
            else if (row != null)
            {
                _dbContext.TenantEnabledIntegrators.Remove(row);
            }

            await _dbContext.SaveChangesAsync();
            return Ok(new { tenantId, integratorId, enabled = request.Enabled });
        }
    }
}
