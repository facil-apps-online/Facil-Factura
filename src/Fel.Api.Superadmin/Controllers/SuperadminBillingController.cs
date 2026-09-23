using System;
using System.Linq;
using System.Threading.Tasks;
using Fel.Core.Entities;
using Fel.Infrastructure.Data;
using Fel.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Fel.Api.Superadmin.Controllers
{
    [ApiController]
    [Route("api/superadmin/billing")]
    public class SuperadminBillingController : ControllerBase
    {
        private readonly FelDbContext _dbContext;
        private readonly BillingMetricsService _billingMetrics;
        private readonly MonthlyBillingCutService _billingCutService;

        public SuperadminBillingController(FelDbContext dbContext, BillingMetricsService billingMetrics, MonthlyBillingCutService billingCutService)
        {
            _dbContext = dbContext;
            _billingMetrics = billingMetrics;
            _billingCutService = billingCutService;
        }

        // 0. Obtener todos los tipos de documento disponibles en la BD
        [HttpGet("document-types")]
        public async Task<IActionResult> GetDocumentTypes()
        {
            var types = await _dbContext.DocumentTypes
                .Select(d => new { d.Code, d.Name, d.GoverningEntity })
                .ToListAsync();
            return Ok(types);
        }

        // 1. Obtener tarifas de un Tenant
        [HttpGet("tenant/{tenantId}/pricing")]
        public async Task<IActionResult> GetTenantPricing(Guid tenantId)
        {
            var pricings = await _dbContext.TenantPricings
                .Include(p => p.DocumentType)
                .Where(p => p.TenantId == tenantId)
                .Select(p => new
                {
                    p.Id,
                    DocumentTypeCode = p.DocumentType!.Code,
                    DocumentTypeName = p.DocumentType.Name,
                    p.PricePerDocument,
                    p.Currency
                })
                .ToListAsync();

            return Ok(pricings);
        }

        // 2. Establecer tarifa de un Tenant
        [HttpPost("tenant/{tenantId}/pricing")]
        public async Task<IActionResult> SetTenantPricing(Guid tenantId, [FromBody] SetPricingRequest request)
        {
            var docType = await _dbContext.DocumentTypes.FirstOrDefaultAsync(d => d.Code == request.DocumentTypeCode);
            if (docType == null) return NotFound("Tipo de documento no válido");

            var pricing = await _dbContext.TenantPricings
                .FirstOrDefaultAsync(p => p.TenantId == tenantId && p.DocumentTypeId == docType.Id);

            if (pricing == null)
            {
                pricing = new TenantPricing
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    DocumentTypeId = docType.Id,
                    PricePerDocument = request.Price,
                    Currency = "COP",
                    UpdatedAt = DateTime.UtcNow
                };
                _dbContext.TenantPricings.Add(pricing);
            }
            else
            {
                pricing.PricePerDocument = request.Price;
                pricing.UpdatedAt = DateTime.UtcNow;
            }

            await _dbContext.SaveChangesAsync();
            return Ok(new 
            { 
                pricing.Id, 
                pricing.TenantId, 
                pricing.DocumentTypeId, 
                pricing.PricePerDocument, 
                pricing.Currency 
            });
        }

        // 3. Ejecutar cálculo de corte mensual (Día 1 del mes) — la lógica real vive en
        // MonthlyBillingCutService para que el disparador automático (Fel.Worker.BillingCutWorker)
        // corra exactamente el mismo código, no una copia.
        [HttpPost("calculate/{year}/{month}")]
        public async Task<IActionResult> CalculateBilling(int year, int month)
        {
            var result = await _billingCutService.RunAsync(year, month);
            if (result.AlreadyExisted)
            {
                return BadRequest("El cálculo para este mes ya fue generado.");
            }

            return Ok(new { Message = $"Corte generado exitosamente para {year}-{month}." });
        }

        // 4. Obtener todos los cortes generados
        [HttpGet("invoices")]
        public async Task<IActionResult> GetInvoices()
        {
            var invoices = await _dbContext.TenantBillings
                .Include(b => b.Tenant)
                .OrderByDescending(b => b.Year)
                .ThenByDescending(b => b.Month)
                .Select(b => new
                {
                    b.Id,
                    TenantName = b.Tenant!.Name,
                    b.Month,
                    b.Year,
                    b.TotalDocuments,
                    b.TotalUsers,
                    b.TotalAmount,
                    b.Currency,
                    b.Status,
                    b.CreatedAt
                })
                .ToListAsync();

            return Ok(invoices);
        }

        // 5. Marcar como pagado
        [HttpPost("invoices/{id}/pay")]
        public async Task<IActionResult> MarkInvoiceAsPaid(Guid id)
        {
            var invoice = await _dbContext.TenantBillings.FindAsync(id);
            if (invoice == null) return NotFound();

            invoice.Status = "PAID";
            await _dbContext.SaveChangesAsync();

            return Ok(new { Message = "Recibo marcado como pagado." });
        }

        // 6. Modo de facturación y tarifa por usuario de un Tenant
        [HttpGet("tenant/{tenantId}/user-pricing")]
        public async Task<IActionResult> GetTenantUserPricing(Guid tenantId)
        {
            var tenant = await _dbContext.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId);
            if (tenant == null) return NotFound();

            var pricing = await _dbContext.TenantUserPricings.FirstOrDefaultAsync(p => p.TenantId == tenantId);

            return Ok(new
            {
                billingMode = tenant.BillingMode.ToString(),
                pricePerUser = pricing?.PricePerUser ?? 0m,
                currency = pricing?.Currency ?? "COP"
            });
        }

        [HttpPut("tenant/{tenantId}/user-pricing")]
        public async Task<IActionResult> SetTenantUserPricing(Guid tenantId, [FromBody] SetUserPricingRequest request)
        {
            var tenant = await _dbContext.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId);
            if (tenant == null) return NotFound();

            if (!Enum.TryParse<TenantBillingMode>(request.BillingMode, out var mode))
            {
                return BadRequest("Modo de facturación inválido.");
            }

            tenant.BillingMode = mode;

            var pricing = await _dbContext.TenantUserPricings.FirstOrDefaultAsync(p => p.TenantId == tenantId);
            if (pricing == null)
            {
                pricing = new TenantUserPricing { Id = Guid.NewGuid(), TenantId = tenantId, Currency = "COP" };
                _dbContext.TenantUserPricings.Add(pricing);
            }
            pricing.PricePerUser = request.PricePerUser;
            pricing.UpdatedAt = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync();
            return Ok(new { Message = "Configuración actualizada." });
        }

        // 7. Catálogo de paquetes prepago de un Tenant
        [HttpGet("tenant/{tenantId}/prepaid-packages")]
        public async Task<IActionResult> GetPrepaidPackages(Guid tenantId)
        {
            var packages = await _dbContext.PrepaidPackages
                .Where(p => p.TenantId == tenantId)
                .Select(p => new { p.Id, p.Name, p.TotalPrice, p.DiscountedPricePerUser, p.IsActive })
                .ToListAsync();

            return Ok(packages);
        }

        [HttpPost("tenant/{tenantId}/prepaid-packages")]
        public async Task<IActionResult> CreatePrepaidPackage(Guid tenantId, [FromBody] PrepaidPackageRequest request)
        {
            if (!await _dbContext.Tenants.AnyAsync(t => t.Id == tenantId)) return NotFound("Tenant no existe.");

            var package = new PrepaidPackage
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Name = request.Name,
                TotalPrice = request.TotalPrice,
                DiscountedPricePerUser = request.DiscountedPricePerUser,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            _dbContext.PrepaidPackages.Add(package);
            await _dbContext.SaveChangesAsync();

            return Ok(new { package.Id, package.Name, package.TotalPrice, package.DiscountedPricePerUser, package.IsActive });
        }

        [HttpPut("prepaid-packages/{id}")]
        public async Task<IActionResult> UpdatePrepaidPackage(Guid id, [FromBody] PrepaidPackageRequest request)
        {
            var package = await _dbContext.PrepaidPackages.FirstOrDefaultAsync(p => p.Id == id);
            if (package == null) return NotFound();

            package.Name = request.Name;
            package.TotalPrice = request.TotalPrice;
            package.DiscountedPricePerUser = request.DiscountedPricePerUser;
            package.IsActive = request.IsActive;

            await _dbContext.SaveChangesAsync();
            return Ok(new { package.Id, package.Name, package.TotalPrice, package.DiscountedPricePerUser, package.IsActive });
        }

        // 8. Tarifa de un Tenant por integrador (override opcional sobre BillingMode/user-pricing
        // de arriba, que siguen siendo el default si no hay fila aquí para un integrador dado)
        [HttpGet("tenant/{tenantId}/integrator-billing")]
        public async Task<IActionResult> GetTenantIntegratorBilling(Guid tenantId)
        {
            var overrides = await _dbContext.TenantIntegratorBillings
                .Include(b => b.Integrator)
                .Where(b => b.TenantId == tenantId)
                .ToDictionaryAsync(b => b.IntegratorId, b => b);

            var integrators = await _dbContext.Integrators
                .Where(i => i.IsActive)
                .Select(i => new { i.Id, i.Code, i.Name })
                .ToListAsync();

            var result = integrators.Select(i => overrides.TryGetValue(i.Id, out var over)
                ? new { i.Id, i.Code, i.Name, HasOverride = true, Mode = over.Mode.ToString(), over.PricePerUser }
                : new { i.Id, i.Code, i.Name, HasOverride = false, Mode = "PerDocument", PricePerUser = 0m });

            return Ok(result);
        }

        [HttpPut("tenant/{tenantId}/integrator-billing/{integratorId}")]
        public async Task<IActionResult> SetTenantIntegratorBilling(Guid tenantId, Guid integratorId, [FromBody] SetTenantIntegratorBillingRequest request)
        {
            if (!await _dbContext.Tenants.AnyAsync(t => t.Id == tenantId)) return NotFound("Tenant no existe.");
            if (!await _dbContext.Integrators.AnyAsync(i => i.Id == integratorId)) return NotFound("Integrador no existe.");

            if (!Enum.TryParse<TenantBillingMode>(request.Mode, out var mode))
            {
                return BadRequest("Modo de facturación inválido.");
            }

            var row = await _dbContext.TenantIntegratorBillings
                .FirstOrDefaultAsync(b => b.TenantId == tenantId && b.IntegratorId == integratorId);

            if (row == null)
            {
                row = new TenantIntegratorBilling { Id = Guid.NewGuid(), TenantId = tenantId, IntegratorId = integratorId };
                _dbContext.TenantIntegratorBillings.Add(row);
            }

            row.Mode = mode;
            row.PricePerUser = request.PricePerUser;

            await _dbContext.SaveChangesAsync();
            return Ok(new { row.Id, row.IntegratorId, Mode = row.Mode.ToString(), row.PricePerUser });
        }

        // Borra el override: el integrador vuelve a usar el default del Tenant (BillingMode/user-pricing)
        [HttpDelete("tenant/{tenantId}/integrator-billing/{integratorId}")]
        public async Task<IActionResult> DeleteTenantIntegratorBilling(Guid tenantId, Guid integratorId)
        {
            var row = await _dbContext.TenantIntegratorBillings
                .FirstOrDefaultAsync(b => b.TenantId == tenantId && b.IntegratorId == integratorId);
            if (row == null) return NotFound();

            _dbContext.TenantIntegratorBillings.Remove(row);
            await _dbContext.SaveChangesAsync();
            return Ok(new { message = "Override eliminado, vuelve al default del Tenant." });
        }
    }

    public class SetPricingRequest
    {
        public string DocumentTypeCode { get; set; } = string.Empty;
        public decimal Price { get; set; }
    }

    public class SetUserPricingRequest
    {
        public string BillingMode { get; set; } = "PerDocument";
        public decimal PricePerUser { get; set; }
    }

    public class PrepaidPackageRequest
    {
        public string Name { get; set; } = string.Empty;
        public decimal TotalPrice { get; set; }
        public decimal DiscountedPricePerUser { get; set; }
        public bool IsActive { get; set; } = true;
    }

    public class SetTenantIntegratorBillingRequest
    {
        public string Mode { get; set; } = "PerDocument";
        public decimal PricePerUser { get; set; }
    }
}
