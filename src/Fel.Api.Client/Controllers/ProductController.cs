using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Fel.Api.Client.Models;
using Fel.Core.Entities;
using Fel.Infrastructure.Data;
using Fel.Api.Security;

namespace Fel.Api.Client.Controllers
{
    [ApiController]
    [Route("api/client/products")]
    [AllowAllBranches]
    public class ProductController : ClientPortalControllerBase
    {
        private readonly FelDbContext _dbContext;

        public ProductController(FelDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        // Página de la lista de Productos: el servidor busca, ordena y corta (hay clientes con miles), y avisa cuántos tienen los impuestos por completar.
        [HttpGet]
        public async Task<IActionResult> GetPage([FromQuery] ProductScope? scope, [FromQuery] string? q, [FromQuery] bool onlyPending,
            [FromQuery] string? sort, [FromQuery] string? dir, [FromQuery] int page = 1, [FromQuery] int pageSize = 25)
        {
            try
            {
                var clientId = GetCurrentClientId();
                var (items, total, pendingCount) = await ProductSearch.PageAsync(_dbContext.Products.AsNoTracking().Where(p => p.ClientId == clientId), scope, q, onlyPending, sort, dir, page, pageSize);
                return Ok(new { items, total, pendingCount });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(ex.Message);
            }
        }

        // Búsqueda para el selector de producto de las líneas de un documento: las primeras coincidencias de lo escrito, con impuestos y unidad de medida.
        [HttpGet("search")]
        public async Task<IActionResult> Search([FromQuery] ProductScope? scope, [FromQuery] string? q, [FromQuery] int limit = ProductSearch.DefaultLimit)
        {
            try
            {
                var clientId = GetCurrentClientId();
                return Ok(await ProductSearch.SearchAsync(_dbContext.Products.AsNoTracking().Where(p => p.ClientId == clientId), scope, q, limit));
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(ex.Message);
            }
        }

        // Los productos de las líneas de un documento que se abre (borrador, nota, detalle): el formulario ya no trae todo el catálogo.
        [HttpGet("by-ids")]
        public async Task<IActionResult> GetByIds([FromQuery] List<Guid> ids)
        {
            try
            {
                var clientId = GetCurrentClientId();
                var wanted = ids.Distinct().Take(ProductSearch.MaxByIds).ToList();
                var products = await _dbContext.Products.AsNoTracking()
                    .Include(p => p.Taxes).Include(p => p.UnitOfMeasure)
                    .Where(p => p.ClientId == clientId && wanted.Contains(p.Id))
                    .ToListAsync();
                return Ok(products);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(ex.Message);
            }
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            try
            {
                var clientId = GetCurrentClientId();
                var product = await _dbContext.Products
                    .Include(p => p.Taxes)
                    .Include(p => p.UnitOfMeasure)
                    .FirstOrDefaultAsync(p => p.Id == id && p.ClientId == clientId);

                if (product == null) return NotFound("Producto no encontrado.");
                return Ok(product);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(ex.Message);
            }
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] Product product)
        {
            try
            {
                var clientId = GetCurrentClientId();
                
                // Validar si ya existe
                var existing = await _dbContext.Products
                    .FirstOrDefaultAsync(p => p.ClientId == clientId && p.Scope == product.Scope && p.Code == product.Code);
                    
                if (existing != null)
                    return BadRequest("Ya existe un producto con este código/SKU.");

                product.Id = Guid.NewGuid();
                product.ClientId = clientId;
                product.TaxesPending = false;
                if (!Enum.IsDefined(product.Scope)) return BadRequest("La familia del producto no es válida.");
                product.CreatedAt = DateTime.UtcNow;
                product.UpdatedAt = DateTime.UtcNow;
                if (product.Scope == ProductScope.Support)
                {
                    product.IvaTreatment = IvaTreatment.Exento;
                    product.IvaRate = 0;
                    product.Taxes.Clear();
                }
                else if (product.IvaTreatment != IvaTreatment.Gravado) product.IvaRate = 0;
                if (product.IvaTreatment != IvaTreatment.Gravado) product.PriceIncludesIva = false;
                if (string.IsNullOrWhiteSpace(product.RetentionGroupKey)) product.RetentionGroupKey = null;

                foreach (var tax in product.Taxes)
                {
                    tax.Id = Guid.NewGuid();
                    tax.ProductId = product.Id;
                    tax.TaxCategory = NormalizeAdditionalTaxCategory(tax.TaxCategory);
                }

                _dbContext.Products.Add(product);
                await _dbContext.SaveChangesAsync();

                return CreatedAtAction(nameof(GetById), new { id = product.Id }, product);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(ex.Message);
            }
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] Product updateData)
        {
            try
            {
                var clientId = GetCurrentClientId();
                var product = await _dbContext.Products
                    .Include(p => p.Taxes)
                    .FirstOrDefaultAsync(p => p.Id == id && p.ClientId == clientId);

                if (product == null) return NotFound("Producto no encontrado.");

                if (!Enum.IsDefined(updateData.Scope)) return BadRequest("La familia del producto no es válida.");

                product.Code = updateData.Code;
                product.Scope = updateData.Scope;
                product.StandardCode = updateData.StandardCode;
                product.Name = updateData.Name;
                product.UnitPrice = updateData.UnitPrice;
                product.UnitOfMeasureId = updateData.UnitOfMeasureId;
                product.IvaTreatment = product.Scope == ProductScope.Support ? IvaTreatment.Exento : updateData.IvaTreatment;
                product.IvaRate = product.Scope == ProductScope.Support ? 0 : updateData.IvaTreatment == IvaTreatment.Gravado ? updateData.IvaRate : 0;
                product.PriceIncludesIva = product.IvaTreatment == IvaTreatment.Gravado && updateData.PriceIncludesIva;
                product.RetentionGroupKey = string.IsNullOrWhiteSpace(updateData.RetentionGroupKey) ? null : updateData.RetentionGroupKey;
                product.UpdatedAt = DateTime.UtcNow;
                product.TaxesPending = false;

                // Se limpia la colección en vez de reasignarla (product.Taxes = nuevaLista), y los
                // nuevos se agregan vía el DbSet en vez de la navegación: reasignar la colección
                // después de un RemoveRange confunde el tracking de EF (fixup de la relación
                // interfiere con el borrado ya marcado) y termina en DbUpdateConcurrencyException
                // al guardar — mismo patrón ya resuelto en InvoiceController para los ítems.
                _dbContext.ProductTaxes.RemoveRange(product.Taxes);
                product.Taxes.Clear();
                foreach (var t in product.Scope == ProductScope.Support ? Enumerable.Empty<ProductTax>() : updateData.Taxes)
                {
                    _dbContext.ProductTaxes.Add(new ProductTax
                    {
                        Id = Guid.NewGuid(),
                        ProductId = product.Id,
                        TaxCategory = NormalizeAdditionalTaxCategory(t.TaxCategory),
                        Rate = t.Rate
                    });
                }

                await _dbContext.SaveChangesAsync();
                return Ok(product);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(ex.Message);
            }
        }

        // ---- Carga masiva de productos: plantilla, vista previa y confirmación (ver ProductImporter).
        [HttpGet("template")]
        public IActionResult DownloadTemplate() =>
            File(Fel.Api.Client.Imports.BulkImportTemplates.Products(), ExcelTemplateHelper.XlsxContentType, "plantilla_productos.xlsx");

        // Valida el archivo completo SIN guardar nada y devuelve, fila por fila, lo que se crearía y lo que se omite.
        [HttpPost("import/preview")]
        [RequestSizeLimit(Fel.Api.Client.Imports.ExcelSheetReader.MaxBytes)]
        public async Task<IActionResult> ImportPreview(IFormFile file, [FromForm] string? options)
        {
            var analysis = await AnalyzeImportAsync(file, options);
            return analysis.Error != null ? BadRequest(analysis.Error) : Ok(analysis.Result!.Preview);
        }

        // Vuelve a validar el mismo archivo y guarda los productos válidos en una sola operación: o se crean todos o ninguno.
        [HttpPost("import/confirm")]
        [RequestSizeLimit(Fel.Api.Client.Imports.ExcelSheetReader.MaxBytes)]
        public async Task<IActionResult> ImportConfirm(IFormFile file, [FromForm] string? options)
        {
            var analysis = await AnalyzeImportAsync(file, options);
            if (analysis.Error != null) return BadRequest(analysis.Error);
            var result = analysis.Result!;
            if (result.Preview.FileErrors.Count > 0) return BadRequest(string.Join(" ", result.Preview.FileErrors));
            if (result.Products.Count == 0) return BadRequest("No hay productos por crear en este archivo.");

            _dbContext.Products.AddRange(result.Products);
            try { await _dbContext.SaveChangesAsync(); }
            catch (DbUpdateException ex)
            {
                // Todo o nada: no se creó ningún producto. Se explica en vez de responder 500.
                return BadRequest("No se pudo guardar la carga; no se creó ningún producto. " + (ex.InnerException?.Message ?? ex.Message));
            }
            return Ok(new Fel.Api.Client.Imports.BulkImportResult
            {
                Created = result.Products.Count,
                Skipped = result.Preview.Rows.Count(r => r.Status != Fel.Api.Client.Imports.ImportStatus.Create)
            });
        }

        private async Task<(Fel.Api.Client.Imports.ProductImportResult? Result, string? Error)> AnalyzeImportAsync(IFormFile? file, string? optionsJson)
        {
            var clientId = GetCurrentClientId();
            if (file == null || file.Length == 0) return (null, "Debes adjuntar un archivo Excel (.xlsx).");
            if (file.Length > Fel.Api.Client.Imports.ExcelSheetReader.MaxBytes) return (null, "El archivo supera los 10 MB.");

            var options = Fel.Api.Client.Imports.BulkImportOptionsParser.Parse(optionsJson);
            if (options == null) return (null, "Las opciones de la carga no son válidas.");
            var scope = ParseScope(Request.Query["scope"]);
            var codes = await _dbContext.Products.AsNoTracking().Where(p => p.ClientId == clientId && p.Scope == scope).Select(p => p.Code).ToListAsync();
            var rates = (await _dbContext.TaxCatalogItems.AsNoTracking().Where(t => t.Kind == TaxCatalogKind.IvaRate && t.IsActive).Select(t => t.Category).ToListAsync())
                .Select(c => decimal.TryParse(c, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : -1).Where(d => d >= 0).ToList();
            var context = new Fel.Api.Client.Imports.ProductImportContext
            {
                ClientId = clientId,
                Scope = scope,
                Units = await _dbContext.UnitsOfMeasure.AsNoTracking().Where(u => u.IsActive).ToListAsync(),
                ExistingCodes = new HashSet<string>(codes, StringComparer.OrdinalIgnoreCase),
                IvaRates = new HashSet<decimal>(rates),
                Options = options
            };

            using var stream = file.OpenReadStream();
            var sheet = Fel.Api.Client.Imports.ExcelSheetReader.Read(stream, Fel.Api.Client.Imports.ProductImporter.Aliases, Fel.Api.Client.Imports.ProductImporter.Required);
            return (Fel.Api.Client.Imports.ProductImporter.Analyze(sheet, context), null);
        }

        public sealed class BulkTaxesRequest
        {
            public List<Guid> Ids { get; set; } = new();
            public IvaTreatment IvaTreatment { get; set; } = IvaTreatment.Gravado;
            public decimal IvaRate { get; set; }
        }

        // Asigna el mismo tratamiento de IVA a varios productos a la vez (los que llegaron de una carga con "impuestos por completar") y los da por completos.
        [HttpPost("bulk-taxes")]
        public async Task<IActionResult> BulkTaxes([FromBody] BulkTaxesRequest request)
        {
            var clientId = GetCurrentClientId();
            if (request.Ids == null || request.Ids.Count == 0) return BadRequest("Selecciona al menos un producto.");
            if (request.Ids.Count > 5000) return BadRequest("Puedes actualizar hasta 5.000 productos a la vez.");
            if (!Enum.IsDefined(request.IvaTreatment)) return BadRequest("El tratamiento de IVA no es válido.");
            if (request.IvaTreatment == IvaTreatment.Gravado && request.IvaRate is <= 0 or > 100) return BadRequest("Indica la tarifa de IVA (mayor que 0 y hasta 100).");

            var products = await _dbContext.Products.Where(p => p.ClientId == clientId && p.Scope == ProductScope.Invoice && request.Ids.Contains(p.Id)).ToListAsync();
            foreach (var product in products)
            {
                product.IvaTreatment = request.IvaTreatment;
                product.IvaRate = request.IvaTreatment == IvaTreatment.Gravado ? request.IvaRate : 0;
                // Si el precio venía con impuestos incluidos solo se conserva cuando el producto queda gravado.
                if (request.IvaTreatment != IvaTreatment.Gravado) product.PriceIncludesIva = false;
                product.TaxesPending = false;
                product.UpdatedAt = DateTime.UtcNow;
            }
            await _dbContext.SaveChangesAsync();
            return Ok(new { updated = products.Count });
        }

        private static ProductScope ParseScope(string? value) =>
            !string.IsNullOrWhiteSpace(value) && Enum.TryParse<ProductScope>(value, true, out var scope)
                ? scope
                : ProductScope.Invoice;

        private static string NormalizeAdditionalTaxCategory(string? category) =>
            string.Equals(category?.Trim(), "IC", StringComparison.OrdinalIgnoreCase)
                ? "IMP_CONSUMO"
                : (category ?? string.Empty).Trim().ToUpperInvariant();

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            try
            {
                var clientId = GetCurrentClientId();
                var product = await _dbContext.Products
                    .FirstOrDefaultAsync(p => p.Id == id && p.ClientId == clientId);

                if (product == null) return NotFound("Producto no encontrado.");

                _dbContext.Products.Remove(product);
                await _dbContext.SaveChangesAsync();
                return NoContent();
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(ex.Message);
            }
        }
    }
}
