using System;
using System.Linq;
using System.Threading.Tasks;
using Fel.Core.Entities;
using Fel.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Fel.Api.Tenant.Controllers
{
    // Tarifa que un Tenant le cobra a una de las sucursales de su Client por UN integrador en particular: override opcional sobre
    // Branch.PricePerDocument — si no hay fila aquí para un integrador dado, ese integrador sigue usando la tarifa plana de la sucursal.
    [ApiController]
    [Route("api/tenant/clients/{clientId}/branches/{branchId}/integrator-billing")]
    public class TenantClientIntegratorBillingController : ControllerBase
    {
        private readonly FelDbContext _dbContext;

        public TenantClientIntegratorBillingController(FelDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        private Guid GetCurrentTenantId()
        {
            if (Request.Headers.TryGetValue("x-tenant-id", out var tenantIdStr) && Guid.TryParse(tenantIdStr, out var tenantId))
            {
                return tenantId;
            }
            throw new UnauthorizedAccessException("x-tenant-id Header is missing");
        }

        // La sucursal debe ser de un Client del tenant.
        private async Task<Branch?> GetOwnedBranchAsync(Guid clientId, Guid branchId)
        {
            var tenantId = GetCurrentTenantId();
            return await _dbContext.Branches.AsNoTracking()
                .FirstOrDefaultAsync(b => b.Id == branchId && b.ClientId == clientId && b.Client.TenantId == tenantId);
        }

        [HttpGet]
        public async Task<IActionResult> GetIntegratorBilling(Guid clientId, Guid branchId)
        {
            try
            {
                var branch = await GetOwnedBranchAsync(clientId, branchId);
                if (branch == null) return NotFound();

                var overrides = await _dbContext.ClientIntegratorBillings
                    .Where(b => b.BranchId == branchId)
                    .ToDictionaryAsync(b => b.IntegratorId, b => b);

                var integrators = await _dbContext.Integrators
                    .Where(i => i.IsActive)
                    .Select(i => new { i.Id, i.Code, i.Name })
                    .ToListAsync();

                var result = integrators.Select(i => overrides.TryGetValue(i.Id, out var over)
                    ? new { i.Id, i.Code, i.Name, HasOverride = true, Mode = over.Mode.ToString(), over.PricePerDocument, over.PricePerUser }
                    : new { i.Id, i.Code, i.Name, HasOverride = false, Mode = "PerDocument", PricePerDocument = branch.PricePerDocument, PricePerUser = 0m });

                return Ok(result);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(ex.Message);
            }
        }

        [HttpPut("{integratorId}")]
        public async Task<IActionResult> SetIntegratorBilling(Guid clientId, Guid branchId, Guid integratorId, [FromBody] SetClientIntegratorBillingRequest request)
        {
            try
            {
                if (await GetOwnedBranchAsync(clientId, branchId) == null) return NotFound();

                if (!await _dbContext.Integrators.AnyAsync(i => i.Id == integratorId)) return NotFound("Integrador no existe.");

                if (!Enum.TryParse<TenantBillingMode>(request.Mode, out var mode))
                {
                    return BadRequest("Modo de facturación inválido.");
                }
                if (request.PricePerDocument < 0 || request.PricePerUser < 0) return BadRequest("Las tarifas no pueden ser negativas.");

                var row = await _dbContext.ClientIntegratorBillings
                    .FirstOrDefaultAsync(b => b.BranchId == branchId && b.IntegratorId == integratorId);

                if (row == null)
                {
                    row = new ClientIntegratorBilling { Id = Guid.NewGuid(), ClientId = clientId, BranchId = branchId, IntegratorId = integratorId };
                    _dbContext.ClientIntegratorBillings.Add(row);
                }

                row.Mode = mode;
                row.PricePerDocument = request.PricePerDocument;
                row.PricePerUser = request.PricePerUser;

                await _dbContext.SaveChangesAsync();
                return Ok(new { row.Id, row.IntegratorId, Mode = row.Mode.ToString(), row.PricePerDocument, row.PricePerUser });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(ex.Message);
            }
        }

        // Borra el override: el integrador vuelve a la tarifa plana de la sucursal.
        [HttpDelete("{integratorId}")]
        public async Task<IActionResult> DeleteIntegratorBilling(Guid clientId, Guid branchId, Guid integratorId)
        {
            try
            {
                if (await GetOwnedBranchAsync(clientId, branchId) == null) return NotFound();

                var row = await _dbContext.ClientIntegratorBillings
                    .FirstOrDefaultAsync(b => b.BranchId == branchId && b.IntegratorId == integratorId);
                if (row == null) return NotFound();

                _dbContext.ClientIntegratorBillings.Remove(row);
                await _dbContext.SaveChangesAsync();
                return Ok(new { message = "Override eliminado, vuelve a la tarifa plana de la sucursal." });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(ex.Message);
            }
        }
    }

    public class SetClientIntegratorBillingRequest
    {
        public string Mode { get; set; } = "PerDocument";
        public decimal PricePerDocument { get; set; }
        public decimal PricePerUser { get; set; }
    }
}
