using System;
using System.Linq;
using System.Threading.Tasks;
using Fel.Core.Entities;
using Fel.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Fel.Api.Superadmin.Controllers
{
    // Administración del motor de retenciones: el catálogo de conceptos (RetefUENTE/ReteIVA, ver
    // RetentionConcept) y los parámetros tributarios con vigencia (UVT, ver TaxParameter). Los
    // tenants/clientes solo leen esto (vía Fel.Api.Client) para calcular automáticamente las
    // retenciones de cada factura.
    [ApiController]
    [Route("api/superadmin/retention-concepts")]
    public class SuperadminRetentionConceptController : ControllerBase
    {
        private readonly FelDbContext _dbContext;

        public SuperadminRetentionConceptController(FelDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var items = await _dbContext.RetentionConcepts
                .OrderBy(c => c.GroupLabel).ThenBy(c => c.PersonType).ThenByDescending(c => c.EffectiveFrom)
                .Select(c => new
                {
                    c.Id,
                    c.GroupKey,
                    c.GroupLabel,
                    c.Name,
                    PersonType = c.PersonType.ToString(),
                    c.TaxCategory,
                    BaseType = c.BaseType.ToString(),
                    c.BaseUvt,
                    c.Rate,
                    c.EffectiveFrom,
                    c.IsActive
                })
                .ToListAsync();

            return Ok(items);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] RetentionConceptRequest request)
        {
            if (!Enum.TryParse<RetentionPersonType>(request.PersonType, out var personType))
                return BadRequest("Tipo de persona inválido. Debe ser Ambas, Natural o Juridica.");
            if (!Enum.TryParse<RetentionBaseType>(request.BaseType, out var baseType))
                return BadRequest("Base de cálculo inválida. Debe ser Subtotal o IvaGenerado.");

            var concept = new RetentionConcept
            {
                Id = Guid.NewGuid(),
                GroupKey = request.GroupKey.Trim().ToUpperInvariant(),
                GroupLabel = request.GroupLabel.Trim(),
                Name = request.Name.Trim(),
                PersonType = personType,
                TaxCategory = request.TaxCategory.Trim().ToUpperInvariant(),
                BaseType = baseType,
                BaseUvt = request.BaseUvt,
                Rate = request.Rate,
                EffectiveFrom = request.EffectiveFrom,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            _dbContext.RetentionConcepts.Add(concept);
            await _dbContext.SaveChangesAsync();
            return Ok(concept);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] RetentionConceptRequest request)
        {
            var concept = await _dbContext.RetentionConcepts.FindAsync(id);
            if (concept == null) return NotFound();

            if (!Enum.TryParse<RetentionPersonType>(request.PersonType, out var personType))
                return BadRequest("Tipo de persona inválido. Debe ser Ambas, Natural o Juridica.");
            if (!Enum.TryParse<RetentionBaseType>(request.BaseType, out var baseType))
                return BadRequest("Base de cálculo inválida. Debe ser Subtotal o IvaGenerado.");

            concept.GroupKey = request.GroupKey.Trim().ToUpperInvariant();
            concept.GroupLabel = request.GroupLabel.Trim();
            concept.Name = request.Name.Trim();
            concept.PersonType = personType;
            concept.TaxCategory = request.TaxCategory.Trim().ToUpperInvariant();
            concept.BaseType = baseType;
            concept.BaseUvt = request.BaseUvt;
            concept.Rate = request.Rate;
            concept.EffectiveFrom = request.EffectiveFrom;
            concept.IsActive = request.IsActive;

            await _dbContext.SaveChangesAsync();
            return Ok(concept);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var concept = await _dbContext.RetentionConcepts.FindAsync(id);
            if (concept == null) return NotFound();

            _dbContext.RetentionConcepts.Remove(concept);
            await _dbContext.SaveChangesAsync();
            return Ok(new { message = "Concepto eliminado." });
        }
    }

    [ApiController]
    [Route("api/superadmin/tax-parameters")]
    public class SuperadminTaxParameterController : ControllerBase
    {
        private readonly FelDbContext _dbContext;

        public SuperadminTaxParameterController(FelDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var items = await _dbContext.TaxParameters
                .OrderBy(p => p.Code).ThenByDescending(p => p.EffectiveFrom)
                .ToListAsync();

            return Ok(items);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] TaxParameterRequest request)
        {
            var parameter = new TaxParameter
            {
                Id = Guid.NewGuid(),
                Code = request.Code.Trim().ToUpperInvariant(),
                Value = request.Value,
                EffectiveFrom = request.EffectiveFrom,
                CreatedAt = DateTime.UtcNow
            };

            _dbContext.TaxParameters.Add(parameter);
            await _dbContext.SaveChangesAsync();
            return Ok(parameter);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var parameter = await _dbContext.TaxParameters.FindAsync(id);
            if (parameter == null) return NotFound();

            _dbContext.TaxParameters.Remove(parameter);
            await _dbContext.SaveChangesAsync();
            return Ok(new { message = "Parámetro eliminado." });
        }
    }

    public class RetentionConceptRequest
    {
        public string GroupKey { get; set; } = string.Empty;
        public string GroupLabel { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string PersonType { get; set; } = "Ambas";
        public string TaxCategory { get; set; } = "RET_FUENTE";
        public string BaseType { get; set; } = "Subtotal";
        public decimal BaseUvt { get; set; }
        public decimal Rate { get; set; }
        public DateTime EffectiveFrom { get; set; }
        public bool IsActive { get; set; } = true;
    }

    public class TaxParameterRequest
    {
        public string Code { get; set; } = string.Empty;
        public decimal Value { get; set; }
        public DateTime EffectiveFrom { get; set; }
    }
}
