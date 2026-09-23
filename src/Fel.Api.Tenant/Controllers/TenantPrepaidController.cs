using System;
using System.Linq;
using System.Threading.Tasks;
using Fel.Core.Entities;
using Fel.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Fel.Api.Tenant.Controllers
{
    [ApiController]
    [Route("api/tenant/prepaid")]
    public class TenantPrepaidController : ControllerBase
    {
        private readonly FelDbContext _dbContext;

        public TenantPrepaidController(FelDbContext dbContext)
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

        // Catálogo de paquetes (bolsas) disponibles para el tenant, definidos por Superadmin
        [HttpGet("packages")]
        public async Task<IActionResult> GetAvailablePackages()
        {
            try
            {
                var tenantId = GetCurrentTenantId();
                var packages = await _dbContext.PrepaidPackages
                    .Where(p => p.TenantId == tenantId && p.IsActive)
                    .Select(p => new { p.Id, p.Name, p.TotalPrice, p.DiscountedPricePerUser })
                    .ToListAsync();

                return Ok(packages);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(ex.Message);
            }
        }

        // Bolsas del tenant (activas e históricas) con su saldo restante
        [HttpGet("bags")]
        public async Task<IActionResult> GetBags()
        {
            try
            {
                var tenantId = GetCurrentTenantId();
                var bags = await _dbContext.TenantPrepaidBags
                    .Include(b => b.Package)
                    .Where(b => b.TenantId == tenantId)
                    .OrderByDescending(b => b.PurchasedAt)
                    .Select(b => new
                    {
                        b.Id,
                        PackageName = b.Package!.Name,
                        b.RemainingBalance,
                        b.DiscountedPricePerUser,
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

        // Comprar/activar una bolsa del catálogo para este tenant
        [HttpPost("bags")]
        public async Task<IActionResult> PurchaseBag([FromBody] PurchaseBagRequest request)
        {
            try
            {
                var tenantId = GetCurrentTenantId();
                var tenant = await _dbContext.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId);
                if (tenant == null || tenant.BillingMode != TenantBillingMode.PerUser)
                {
                    return BadRequest("Este tenant no está en modo de facturación por usuario.");
                }

                var package = await _dbContext.PrepaidPackages.FirstOrDefaultAsync(p => p.Id == request.PackageId && p.TenantId == tenantId && p.IsActive);
                if (package == null) return BadRequest("Paquete no válido para este tenant.");

                var bag = new TenantPrepaidBag
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    PackageId = package.Id,
                    RemainingBalance = package.TotalPrice,
                    DiscountedPricePerUser = package.DiscountedPricePerUser,
                    AmountPaid = package.TotalPrice,
                    Status = PrepaidBagStatus.Active,
                    PurchasedAt = DateTime.UtcNow
                };

                _dbContext.TenantPrepaidBags.Add(bag);
                await _dbContext.SaveChangesAsync();

                return Ok(new { bag.Id, bag.RemainingBalance, bag.DiscountedPricePerUser, bag.AmountPaid });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(ex.Message);
            }
        }
    }

    public class PurchaseBagRequest
    {
        public Guid PackageId { get; set; }
    }
}
