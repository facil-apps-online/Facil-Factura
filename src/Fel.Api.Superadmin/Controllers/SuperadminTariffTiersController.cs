using System;
using System.Linq;
using System.Threading.Tasks;
using Fel.Core.Entities;
using Fel.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Fel.Api.Superadmin.Controllers
{
    // Tarifario por volumen que Superadmin le cobra a los Tenants en modo PerDocument
    // (BillingMetricsService.GetSuperadminTariffForVolumeAsync). Un tier sin IntegratorId es
    // global (aplica a cualquier integrador que no tenga uno propio para ese rango); uno con
    // IntegratorId solo aplica a ese integrador y tiene prioridad sobre el global.
    [ApiController]
    [Route("api/superadmin/tariff-tiers")]
    public class SuperadminTariffTiersController : ControllerBase
    {
        private readonly FelDbContext _dbContext;

        public SuperadminTariffTiersController(FelDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        [HttpGet]
        public async Task<IActionResult> GetTiers()
        {
            var tiers = await _dbContext.TariffTiers
                .Include(t => t.Integrator)
                .OrderBy(t => t.IntegratorId == null ? 0 : 1).ThenBy(t => t.MinDocuments)
                .Select(t => new
                {
                    t.Id,
                    t.Name,
                    t.MinDocuments,
                    t.MaxDocuments,
                    t.PricePerDocument,
                    t.IsActive,
                    t.IntegratorId,
                    IntegratorName = t.Integrator != null ? t.Integrator.Name : null
                })
                .ToListAsync();

            return Ok(tiers);
        }

        [HttpPost]
        public async Task<IActionResult> CreateTier([FromBody] TariffTierRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Name) || request.MinDocuments < 0 || request.PricePerDocument <= 0)
            {
                return BadRequest("Nombre, rango mínimo y tarifa por documento son obligatorios.");
            }

            if (request.IntegratorId.HasValue && !await _dbContext.Integrators.AnyAsync(i => i.Id == request.IntegratorId))
            {
                return BadRequest("Integrador inválido.");
            }

            var tier = new TariffTier
            {
                Id = Guid.NewGuid(),
                Name = request.Name,
                MinDocuments = request.MinDocuments,
                MaxDocuments = request.MaxDocuments,
                PricePerDocument = request.PricePerDocument,
                IntegratorId = request.IntegratorId,
                IsActive = true
            };

            _dbContext.TariffTiers.Add(tier);
            await _dbContext.SaveChangesAsync();

            return Ok(new { tier.Id, tier.Name, tier.MinDocuments, tier.MaxDocuments, tier.PricePerDocument, tier.IsActive, tier.IntegratorId });
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateTier(Guid id, [FromBody] TariffTierRequest request)
        {
            var tier = await _dbContext.TariffTiers.FirstOrDefaultAsync(t => t.Id == id);
            if (tier == null) return NotFound();

            if (string.IsNullOrWhiteSpace(request.Name) || request.MinDocuments < 0 || request.PricePerDocument <= 0)
            {
                return BadRequest("Nombre, rango mínimo y tarifa por documento son obligatorios.");
            }

            tier.Name = request.Name;
            tier.MinDocuments = request.MinDocuments;
            tier.MaxDocuments = request.MaxDocuments;
            tier.PricePerDocument = request.PricePerDocument;
            tier.IsActive = request.IsActive;
            // El integrador de un tier no se cambia después de creado — si hace falta moverlo,
            // se borra y se crea uno nuevo, para no dejar en el aire cortes ya calculados con él.

            await _dbContext.SaveChangesAsync();
            return Ok(new { tier.Id, tier.Name, tier.MinDocuments, tier.MaxDocuments, tier.PricePerDocument, tier.IsActive, tier.IntegratorId });
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteTier(Guid id)
        {
            var tier = await _dbContext.TariffTiers.FirstOrDefaultAsync(t => t.Id == id);
            if (tier == null) return NotFound();

            _dbContext.TariffTiers.Remove(tier);
            await _dbContext.SaveChangesAsync();
            return Ok(new { message = "Tier eliminado." });
        }
    }

    public class TariffTierRequest
    {
        public string Name { get; set; } = string.Empty;
        public int MinDocuments { get; set; }
        public int? MaxDocuments { get; set; }
        public decimal PricePerDocument { get; set; }
        public bool IsActive { get; set; } = true;
        public Guid? IntegratorId { get; set; }
    }
}
