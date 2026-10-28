using System;
using System.Linq;
using System.Threading.Tasks;
using Fel.Core.Entities;
using Fel.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Fel.Api.Superadmin.Controllers
{
    // Nombres cortos de las categorías de retención (RETE FUENTE, RETE ICA, RETE IVA). Con la tarifa arman el texto corto de cada retención en los selectores
    // de los formularios (según el estilo que elija el cliente o su tenant) y en el PDF de la factura y del documento soporte.
    [ApiController]
    [Route("api/superadmin/retention-short-names")]
    public class SuperadminRetentionShortNamesController : ControllerBase
    {
        private readonly FelDbContext _dbContext;

        public SuperadminRetentionShortNamesController(FelDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll() =>
            Ok(await _dbContext.RetentionShortNames.AsNoTracking().OrderBy(r => r.TaxCategory).Select(r => new { r.Id, r.TaxCategory, r.ShortName }).ToListAsync());

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] RetentionShortNameRequest request)
        {
            var name = (request.ShortName ?? string.Empty).Trim();
            if (name.Length == 0) return BadRequest(new { message = "Escribe el nombre corto." });
            if (name.Length > RetentionShortName.MaxLength) return BadRequest(new { message = $"El nombre corto admite hasta {RetentionShortName.MaxLength} caracteres." });
            if (name.Any(char.IsControl)) return BadRequest(new { message = "El nombre corto tiene caracteres no válidos." });

            var item = await _dbContext.RetentionShortNames.FirstOrDefaultAsync(r => r.Id == id);
            if (item == null) return NotFound(new { message = "Categoría de retención no encontrada." });

            item.ShortName = name;
            await _dbContext.SaveChangesAsync();
            return Ok(new { item.Id, item.TaxCategory, item.ShortName });
        }
    }

    public class RetentionShortNameRequest
    {
        public string? ShortName { get; set; }
    }
}
