using System;
using System.Linq;
using System.Threading.Tasks;
using Fel.Core.Entities;
using Fel.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Fel.Api.Superadmin.Controllers
{
    // Catálogo global de Unidades de Medida DIAN. Los tenants/clientes solo leen esta lista
    // (vía Fel.Api.Client) y seleccionan de ella al crear un producto, en vez de escribir el
    // código a mano — mismo criterio que SuperadminTaxCatalogController.
    [ApiController]
    [Route("api/superadmin/units-of-measure")]
    public class SuperadminUnitOfMeasureController : ControllerBase
    {
        private readonly FelDbContext _dbContext;

        public SuperadminUnitOfMeasureController(FelDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var items = await _dbContext.UnitsOfMeasure
                .OrderBy(u => u.Name)
                .ToListAsync();

            return Ok(items);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] UnitOfMeasureRequest request)
        {
            var dianCode = request.DianCode.Trim().ToUpperInvariant();
            if (await _dbContext.UnitsOfMeasure.AnyAsync(u => u.DianCode == dianCode))
            {
                return BadRequest("Ya existe una unidad con ese código DIAN.");
            }

            var item = new UnitOfMeasure
            {
                Id = Guid.NewGuid(),
                DianCode = dianCode,
                Abbreviation = request.Abbreviation.Trim().ToUpperInvariant(),
                Name = request.Name.Trim(),
                DisplayFormat = request.DisplayFormat,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            _dbContext.UnitsOfMeasure.Add(item);
            await _dbContext.SaveChangesAsync();

            return Ok(item);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] UnitOfMeasureRequest request)
        {
            var item = await _dbContext.UnitsOfMeasure.FindAsync(id);
            if (item == null) return NotFound();

            item.DianCode = request.DianCode.Trim().ToUpperInvariant();
            item.Abbreviation = request.Abbreviation.Trim().ToUpperInvariant();
            item.Name = request.Name.Trim();
            item.DisplayFormat = request.DisplayFormat;
            item.IsActive = request.IsActive;

            await _dbContext.SaveChangesAsync();
            return Ok(item);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var item = await _dbContext.UnitsOfMeasure.FindAsync(id);
            if (item == null) return NotFound();

            if (await _dbContext.Products.AnyAsync(p => p.UnitOfMeasureId == id))
            {
                return BadRequest("Hay productos usando esta unidad — desactívala en vez de borrarla.");
            }

            _dbContext.UnitsOfMeasure.Remove(item);
            await _dbContext.SaveChangesAsync();

            return Ok(new { message = "Unidad eliminada." });
        }
    }

    public class UnitOfMeasureRequest
    {
        public string DianCode { get; set; } = string.Empty;
        public string Abbreviation { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string DisplayFormat { get; set; } = "Combined";
        public bool IsActive { get; set; } = true;
    }
}
