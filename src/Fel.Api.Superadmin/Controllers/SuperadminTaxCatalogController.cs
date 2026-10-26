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
            var items = await _dbContext.TaxCatalogItems
                .OrderBy(i => i.Kind).ThenBy(i => i.Category).ThenBy(i => i.Rate)
                .Select(i => new { i.Id, i.Category, i.Name, Kind = i.Kind.ToString(), i.Rate, i.WordsName, i.DianCode, i.IsActive })
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

            var wordsName = NormalizeWordsName(kind, request.WordsName, out var wordsError);
            if (wordsError != null) return BadRequest(wordsError);

            var dianCode = NormalizeDianCode(kind, request.DianCode, out var dianCodeError);
            if (dianCodeError != null) return BadRequest(dianCodeError);

            var category = request.Category.Trim().ToUpperInvariant();
            if (await _dbContext.TaxCatalogItems.AnyAsync(i => i.Category == category && i.Kind == kind && i.Rate == request.Rate))
            {
                return BadRequest(kind == TaxCatalogKind.Retention
                    ? "Ya existe esa categoría con esa misma tarifa."
                    : "Ya existe una categoría con ese código para ese tipo.");
            }

            var item = new TaxCatalogItem
            {
                Id = Guid.NewGuid(),
                Category = category,
                Name = request.Name.Trim(),
                Kind = kind,
                Rate = request.Rate,
                WordsName = wordsName,
                DianCode = dianCode,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            _dbContext.TaxCatalogItems.Add(item);
            await _dbContext.SaveChangesAsync();

            return Ok(item);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] TaxCatalogItemRequest request)
        {
            var item = await _dbContext.TaxCatalogItems.FindAsync(id);
            if (item == null) return NotFound();

            if (!Enum.TryParse<TaxCatalogKind>(request.Kind, out var kind))
            {
                return BadRequest("Tipo inválido. Debe ser Retention u OtherTax.");
            }

            var wordsName = NormalizeWordsName(kind, request.WordsName, out var wordsError);
            if (wordsError != null) return BadRequest(wordsError);

            var dianCode = NormalizeDianCode(kind, request.DianCode, out var dianCodeError);
            if (dianCodeError != null) return BadRequest(dianCodeError);

            item.Category = request.Category.Trim().ToUpperInvariant();
            item.Name = request.Name.Trim();
            item.Kind = kind;
            item.Rate = request.Rate;
            item.WordsName = wordsName;
            // En los demás tipos DianCode guarda otra cosa (p. ej. el motivo de Dataico de las notas) que esta pantalla no edita: se conserva.
            if (kind == TaxCatalogKind.PaymentMeans) item.DianCode = dianCode;
            item.IsActive = request.IsActive;

            await _dbContext.SaveChangesAsync();
            return Ok(item);
        }

        // El nombre en letras solo existe en las monedas y allí es obligatorio: sin él el valor en letras de una factura en esa moneda no sabría
        // cómo nombrarla. En los demás tipos se descarta.
        private static string? NormalizeWordsName(TaxCatalogKind kind, string? value, out string? error)
        {
            error = null;
            if (kind != TaxCatalogKind.Currency) return null;
            var name = value?.Trim().ToUpperInvariant();
            if (string.IsNullOrEmpty(name))
            {
                error = "La moneda necesita el nombre en letras (por ejemplo PESOS, DÓLARES, EUROS).";
                return null;
            }
            if (name.Length > 50) error = "El nombre en letras no puede pasar de 50 caracteres.";
            return name;
        }

        // El código DIAN solo se captura en los medios de pago (tabla de medios de pago del anexo técnico: "10", "45", "ZZZ"). La emisión
        // directa lo envía en el XML, así que no puede quedar vacío. En los demás tipos no se pide.
        private static string? NormalizeDianCode(TaxCatalogKind kind, string? value, out string? error)
        {
            error = null;
            if (kind != TaxCatalogKind.PaymentMeans) return null;
            var code = value?.Trim().ToUpperInvariant();
            if (string.IsNullOrEmpty(code))
            {
                error = "El medio de pago necesita el código DIAN (tabla de medios de pago del anexo técnico, por ejemplo 10, 45 o ZZZ).";
                return null;
            }
            if (code.Length > 3 || !code.All(char.IsLetterOrDigit)) error = "El código DIAN debe tener hasta 3 letras o números (por ejemplo 10, 45 o ZZZ).";
            return code;
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var item = await _dbContext.TaxCatalogItems.FindAsync(id);
            if (item == null) return NotFound();

            _dbContext.TaxCatalogItems.Remove(item);
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
        public string? WordsName { get; set; }
        public string? DianCode { get; set; }
        public bool IsActive { get; set; } = true;
    }
}
