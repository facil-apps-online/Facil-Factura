using System;
using System.Linq;
using System.Threading.Tasks;
using Fel.Core.Entities;
using Fel.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Fel.Api.Tenant.Controllers
{
    // Paquetes prepago que un Tenant ofrece a uno de sus Clients (Fase 3d) — mismo mecanismo que
    // PrepaidPackage/TenantPrepaidBag entre Superadmin y Tenant, un nivel más abajo. A diferencia
    // de ese caso (donde el Tenant se autoactiva la bolsa desde su propio portal), aquí es el
    // Tenant quien define el catálogo Y activa la bolsa a nombre de su Client — no existe hoy un
    // portal de autoservicio de facturación para el Client.
    [ApiController]
    [Route("api/tenant/clients/{clientId}/prepaid")]
    public class TenantClientPrepaidController : ControllerBase
    {
        private readonly FelDbContext _dbContext;

        public TenantClientPrepaidController(FelDbContext dbContext)
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

        private async Task<bool> ClientBelongsToTenantAsync(Guid clientId)
        {
            var tenantId = GetCurrentTenantId();
            return await _dbContext.Clients.AnyAsync(c => c.Id == clientId && c.TenantId == tenantId);
        }

        [HttpGet("packages")]
        public async Task<IActionResult> GetPackages(Guid clientId)
        {
            try
            {
                if (!await ClientBelongsToTenantAsync(clientId)) return NotFound();

                var packages = await _dbContext.ClientPrepaidPackages
                    .Include(p => p.Integrator)
                    .Where(p => p.ClientId == clientId)
                    .Select(p => new
                    {
                        p.Id,
                        p.Name,
                        p.TotalPrice,
                        p.DiscountedPricePerDocument,
                        p.IsActive,
                        IntegratorId = p.IntegratorId,
                        IntegratorName = p.Integrator!.Name
                    })
                    .ToListAsync();

                return Ok(packages);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(ex.Message);
            }
        }

        [HttpPost("packages")]
        public async Task<IActionResult> CreatePackage(Guid clientId, [FromBody] ClientPrepaidPackageRequest request)
        {
            try
            {
                if (!await ClientBelongsToTenantAsync(clientId)) return NotFound();

                if (string.IsNullOrWhiteSpace(request.Name) || request.TotalPrice <= 0 || request.DiscountedPricePerDocument <= 0)
                {
                    return BadRequest("Nombre, precio total y tarifa por documento son obligatorios y deben ser mayores a cero.");
                }

                if (!await _dbContext.Integrators.AnyAsync(i => i.Id == request.IntegratorId))
                {
                    return BadRequest("Integrador inválido.");
                }

                var package = new ClientPrepaidPackage
                {
                    Id = Guid.NewGuid(),
                    ClientId = clientId,
                    IntegratorId = request.IntegratorId,
                    Name = request.Name,
                    TotalPrice = request.TotalPrice,
                    DiscountedPricePerDocument = request.DiscountedPricePerDocument,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };

                _dbContext.ClientPrepaidPackages.Add(package);
                await _dbContext.SaveChangesAsync();

                return Ok(new { package.Id, package.Name, package.TotalPrice, package.DiscountedPricePerDocument, package.IsActive, package.IntegratorId });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(ex.Message);
            }
        }

        [HttpPut("packages/{id}")]
        public async Task<IActionResult> UpdatePackage(Guid clientId, Guid id, [FromBody] ClientPrepaidPackageRequest request)
        {
            try
            {
                if (!await ClientBelongsToTenantAsync(clientId)) return NotFound();

                var package = await _dbContext.ClientPrepaidPackages.FirstOrDefaultAsync(p => p.Id == id && p.ClientId == clientId);
                if (package == null) return NotFound();

                if (string.IsNullOrWhiteSpace(request.Name) || request.TotalPrice <= 0 || request.DiscountedPricePerDocument <= 0)
                {
                    return BadRequest("Nombre, precio total y tarifa por documento son obligatorios y deben ser mayores a cero.");
                }

                package.Name = request.Name;
                package.TotalPrice = request.TotalPrice;
                package.DiscountedPricePerDocument = request.DiscountedPricePerDocument;
                package.IsActive = request.IsActive;
                // El integrador del paquete no se puede cambiar una vez creado: las bolsas ya
                // activadas copiaron el IntegratorId original y deben seguir descontando ese mismo
                // integrador — cambiarlo aquí las dejaría inconsistentes con el catálogo.

                await _dbContext.SaveChangesAsync();

                return Ok(new { package.Id, package.Name, package.TotalPrice, package.DiscountedPricePerDocument, package.IsActive, package.IntegratorId });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(ex.Message);
            }
        }

        [HttpGet("bags")]
        public async Task<IActionResult> GetBags(Guid clientId)
        {
            try
            {
                if (!await ClientBelongsToTenantAsync(clientId)) return NotFound();

                var bags = await _dbContext.ClientPrepaidBags
                    .Include(b => b.Package)
                    .Include(b => b.Integrator)
                    .Where(b => b.ClientId == clientId)
                    .OrderByDescending(b => b.PurchasedAt)
                    .Select(b => new
                    {
                        b.Id,
                        PackageName = b.Package!.Name,
                        IntegratorName = b.Integrator!.Name,
                        b.RemainingBalance,
                        b.DiscountedPricePerDocument,
                        b.AmountPaid,
                        Status = b.Status.ToString(),
                        b.PurchasedAt
                    })
                    .ToListAsync();

                return Ok(bags);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(ex.Message);
            }
        }

        // Activa una bolsa a nombre del Client a partir de un paquete del catálogo — se registra
        // aquí mismo en el momento en que el Tenant cierra la venta (p.ej. tras una reunión
        // comercial), no es una compra en línea que haga el Client.
        [HttpPost("bags")]
        public async Task<IActionResult> ActivateBag(Guid clientId, [FromBody] ActivateClientBagRequest request)
        {
            try
            {
                if (!await ClientBelongsToTenantAsync(clientId)) return NotFound();

                var package = await _dbContext.ClientPrepaidPackages
                    .FirstOrDefaultAsync(p => p.Id == request.PackageId && p.ClientId == clientId && p.IsActive);
                if (package == null) return BadRequest("Paquete no válido para este Client.");

                var bag = new ClientPrepaidBag
                {
                    Id = Guid.NewGuid(),
                    ClientId = clientId,
                    PackageId = package.Id,
                    IntegratorId = package.IntegratorId,
                    RemainingBalance = package.TotalPrice,
                    DiscountedPricePerDocument = package.DiscountedPricePerDocument,
                    AmountPaid = package.TotalPrice,
                    Status = PrepaidBagStatus.Active,
                    PurchasedAt = DateTime.UtcNow
                };

                _dbContext.ClientPrepaidBags.Add(bag);
                await _dbContext.SaveChangesAsync();

                return Ok(new { bag.Id, bag.RemainingBalance, bag.DiscountedPricePerDocument, bag.AmountPaid });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(ex.Message);
            }
        }
    }

    public class ClientPrepaidPackageRequest
    {
        public string Name { get; set; } = string.Empty;
        public decimal TotalPrice { get; set; }
        public decimal DiscountedPricePerDocument { get; set; }
        public bool IsActive { get; set; } = true;
        public Guid IntegratorId { get; set; }
    }

    public class ActivateClientBagRequest
    {
        public Guid PackageId { get; set; }
    }
}
