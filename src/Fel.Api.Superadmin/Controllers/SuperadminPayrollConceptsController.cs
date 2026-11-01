using System;
using System.Linq;
using System.Threading.Tasks;
using Fel.Core.Entities;
using Fel.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Fel.Api.Superadmin.Controllers
{
    // Catálogo de conceptos de nómina (devengos y deducciones): código Dataico, equivalencia en el XML de la DIAN y cómo se captura.
    [ApiController]
    [Route("api/superadmin/payroll-concepts")]
    public class SuperadminPayrollConceptsController : ControllerBase
    {
        private readonly FelDbContext _dbContext;

        public SuperadminPayrollConceptsController(FelDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public class PayrollConceptRequest
        {
            public PayrollConceptSection Section { get; set; }
            public string DataicoCode { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public string DianElement { get; set; } = string.Empty;
            public PayrollDianShape DianShape { get; set; }
            public string? DianAmountAttribute { get; set; }
            public string? DianNonSalaryAttribute { get; set; }
            public string? DianQuantityAttribute { get; set; }
            public string? DianPercentageAttribute { get; set; }
            public PayrollQuantityKind QuantityKind { get; set; }
            public bool HasPercentage { get; set; }
            public bool AllowsAmount { get; set; } = true;
            public bool AllowsNonSalary { get; set; }
            public bool RequiresDescription { get; set; }
            public PayrollSpecialInput SpecialInput { get; set; }
            public int SortOrder { get; set; }
            public bool IsActive { get; set; } = true;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var items = await _dbContext.PayrollConceptTypes.AsNoTracking()
                .OrderBy(t => t.Section).ThenBy(t => t.SortOrder).ThenBy(t => t.Name)
                .ToListAsync();
            return Ok(items);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] PayrollConceptRequest request)
        {
            var error = await ValidateAsync(request, null);
            if (error != null) return BadRequest(error);

            var item = new PayrollConceptType { Id = Guid.NewGuid() };
            Apply(item, request);
            _dbContext.PayrollConceptTypes.Add(item);
            await _dbContext.SaveChangesAsync();
            return Ok(item);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] PayrollConceptRequest request)
        {
            var item = await _dbContext.PayrollConceptTypes.FindAsync(id);
            if (item == null) return NotFound();

            var error = await ValidateAsync(request, id);
            if (error != null) return BadRequest(error);

            Apply(item, request);
            await _dbContext.SaveChangesAsync();
            return Ok(item);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var item = await _dbContext.PayrollConceptTypes.FindAsync(id);
            if (item == null) return NotFound();

            // Los borradores guardan el código del concepto, no una referencia a esta tabla: se puede eliminar sin tocar documentos.
            _dbContext.PayrollConceptTypes.Remove(item);
            await _dbContext.SaveChangesAsync();
            return Ok(new { message = "Concepto de nómina eliminado" });
        }

        // Sin la equivalencia DIAN completa un concepto no se podría emitir por el camino nativo, así que no se deja guardar a medias.
        private async Task<string?> ValidateAsync(PayrollConceptRequest request, Guid? currentId)
        {
            var code = request.DataicoCode.Trim().ToUpperInvariant();
            if (code.Length == 0) return "El código de Dataico es obligatorio.";
            if (request.Name.Trim().Length == 0) return "El nombre es obligatorio.";
            if (request.DianElement.Trim().Length == 0) return "El elemento del XML de la DIAN es obligatorio.";
            if (request.DianShape != PayrollDianShape.Value && request.AllowsAmount && string.IsNullOrWhiteSpace(request.DianAmountAttribute))
                return "Un concepto con valor y forma de atributos o lista necesita el atributo del valor en el XML de la DIAN.";
            if (request.QuantityKind == PayrollQuantityKind.None && request.HasPercentage)
                return "El porcentaje de recargo acompaña a una cantidad en horas.";
            if (await _dbContext.PayrollConceptTypes.AnyAsync(t => t.Section == request.Section && t.DataicoCode == code && t.Id != currentId))
                return "Ya existe un concepto con ese código en la misma sección.";
            return null;
        }

        private static void Apply(PayrollConceptType item, PayrollConceptRequest request)
        {
            static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

            item.Section = request.Section;
            item.DataicoCode = request.DataicoCode.Trim().ToUpperInvariant();
            item.Name = request.Name.Trim();
            item.DianElement = request.DianElement.Trim();
            item.DianShape = request.DianShape;
            item.DianAmountAttribute = Blank(request.DianAmountAttribute);
            item.DianNonSalaryAttribute = Blank(request.DianNonSalaryAttribute);
            item.DianQuantityAttribute = Blank(request.DianQuantityAttribute);
            item.DianPercentageAttribute = Blank(request.DianPercentageAttribute);
            item.QuantityKind = request.QuantityKind;
            item.HasPercentage = request.HasPercentage;
            item.AllowsAmount = request.AllowsAmount;
            item.AllowsNonSalary = request.AllowsNonSalary;
            item.RequiresDescription = request.RequiresDescription;
            item.SpecialInput = request.SpecialInput;
            item.SortOrder = request.SortOrder;
            item.IsActive = request.IsActive;
        }
    }
}
