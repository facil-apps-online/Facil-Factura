using System;
using System.Linq;
using System.Threading.Tasks;
using Fel.Core.Entities;
using Fel.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Fel.Api.Superadmin.Controllers
{
    // Catálogo global de categorías de impuestos/retenciones de Dataico. Los tenants/clientes
    // solo leen esta lista (vía Fel.Api.Client) y seleccionan de ella en vez de escribir el
    // código a mano.
    [ApiController]
    [Route("api/superadmin/tax-catalog")]
    public class SuperadminTaxCatalogController : ControllerBase
    {
        private readonly FelDbContext _dbContext;

        public SuperadminTaxCatalogController(FelDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var items = await _dbContext.DataicoTaxCatalogItems
                .OrderBy(i => i.Kind).ThenBy(i => i.Category).ThenBy(i => i.Rate)
                .Select(i => new { i.Id, i.Category, i.Name, Kind = i.Kind.ToString(), i.Rate, i.IsActive })
                .ToListAsync();

            return Ok(items);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] TaxCatalogItemRequest request)
        {
            if (!Enum.TryParse<TaxCatalogKind>(request.Kind, out var kind))
            {
                return BadRequest("Tipo inválido. Debe ser Retention u OtherTax.");
            }

            var category = request.Category.Trim().ToUpperInvariant();
            if (await _dbContext.DataicoTaxCatalogItems.AnyAsync(i => i.Category == category && i.Kind == kind && i.Rate == request.Rate))
            {
                return BadRequest(kind == TaxCatalogKind.Retention
                    ? "Ya existe esa categoría con esa misma tarifa."
                    : "Ya existe una categoría con ese código para ese tipo.");
            }

            var item = new DataicoTaxCatalogItem
            {
                Id = Guid.NewGuid(),
                Category = category,
                Name = request.Name.Trim(),
                Kind = kind,
                Rate = request.Rate,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            _dbContext.DataicoTaxCatalogItems.Add(item);
            await _dbContext.SaveChangesAsync();

            return Ok(item);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] TaxCatalogItemRequest request)
        {
            var item = await _dbContext.DataicoTaxCatalogItems.FindAsync(id);
            if (item == null) return NotFound();

            if (!Enum.TryParse<TaxCatalogKind>(request.Kind, out var kind))
            {
                return BadRequest("Tipo inválido. Debe ser Retention u OtherTax.");
            }

            item.Category = request.Category.Trim().ToUpperInvariant();
            item.Name = request.Name.Trim();
            item.Kind = kind;
            item.Rate = request.Rate;
            item.IsActive = request.IsActive;

            await _dbContext.SaveChangesAsync();
            return Ok(item);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var item = await _dbContext.DataicoTaxCatalogItems.FindAsync(id);
            if (item == null) return NotFound();

            _dbContext.DataicoTaxCatalogItems.Remove(item);
            await _dbContext.SaveChangesAsync();

            return Ok(new { message = "Categoría eliminada." });
        }
    }

    public class TaxCatalogItemRequest
    {
        public string Category { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Kind { get; set; } = "Retention";
        public decimal? Rate { get; set; }
        public bool IsActive { get; set; } = true;
    }
}
