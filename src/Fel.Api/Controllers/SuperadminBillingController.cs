using System;
using System.Linq;
using System.Threading.Tasks;
using Fel.Core.Entities;
using Fel.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Fel.Api.Controllers
{
    [ApiController]
    [Route("api/superadmin/billing")]
    public class SuperadminBillingController : ControllerBase
    {
        private readonly FelDbContext _dbContext;

        public SuperadminBillingController(FelDbContext dbContext)
        {
            _dbContext = dbContext;
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

        // 3. Ejecutar cálculo de corte mensual (Día 1 del mes)
        [HttpPost("calculate/{year}/{month}")]
        public async Task<IActionResult> CalculateBilling(int year, int month)
        {
            // Validar que no exista ya el corte
            var existingBilling = await _dbContext.TenantBillings
                .Where(b => b.Year == year && b.Month == month)
                .ToListAsync();

            if (existingBilling.Any())
            {
                return BadRequest("El cálculo para este mes ya fue generado.");
            }

            var startDate = new DateTime(year, month, 1);
            var endDate = startDate.AddMonths(1); // Hasta el día 1 del siguiente mes

            // Obtener todos los documentos exitosos del mes
            var consumedDocs = await _dbContext.Documents
                .Where(d => d.Status == "APPROVED" && d.ProcessedAt >= startDate && d.ProcessedAt < endDate)
                .Include(d => d.Client)
                .ToListAsync();

            // Agrupar por Tenant
            var docsByTenant = consumedDocs.GroupBy(d => d.Client.TenantId);

            foreach (var group in docsByTenant)
            {
                var tenantId = group.Key;
                decimal totalAmount = 0;
                int totalDocs = group.Count();

                // Sumar los PriceCharged (En producción real, el Worker graba el PriceCharged al procesar. 
                // Si recalculamos en lote, leeríamos de TenantPricings).
                // Asumiremos que el Worker grabó el precio correcto en 'PriceCharged'
                totalAmount = group.Sum(d => d.PriceCharged);

                var billing = new TenantBilling
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    Month = month,
                    Year = year,
                    TotalDocuments = totalDocs,
                    TotalAmount = totalAmount,
                    Currency = "COP",
                    Status = "Pending",
                    CreatedAt = DateTime.UtcNow
                };

                _dbContext.TenantBillings.Add(billing);
            }

            await _dbContext.SaveChangesAsync();

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
    }

    public class SetPricingRequest
    {
        public string DocumentTypeCode { get; set; } = string.Empty;
        public decimal Price { get; set; }
    }
}
