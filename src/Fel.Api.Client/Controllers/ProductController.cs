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

        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] ProductScope? scope = null)
        {
            try
            {
                var clientId = GetCurrentClientId();
                var products = await _dbContext.Products
                    .Include(p => p.Taxes)
                    .Include(p => p.UnitOfMeasure)
                    .Where(p => p.ClientId == clientId && (!scope.HasValue || p.Scope == scope.Value))
                    .OrderByDescending(p => p.CreatedAt)
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
                if (string.IsNullOrWhiteSpace(product.RetentionGroupKey)) product.RetentionGroupKey = null;

                foreach (var tax in product.Taxes)
                {
                    tax.Id = Guid.NewGuid();
                    tax.ProductId = product.Id;
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
                product.RetentionGroupKey = string.IsNullOrWhiteSpace(updateData.RetentionGroupKey) ? null : updateData.RetentionGroupKey;
                product.UpdatedAt = DateTime.UtcNow;

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
                        TaxCategory = t.TaxCategory,
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

        [HttpGet("template")]
        public IActionResult DownloadTemplate()
        {
            using var workbook = new XLWorkbook();
            var sheet = workbook.Worksheets.Add("Productos");

            ExcelTemplateHelper.WriteHeaderRow(sheet, 1,
                "Codigo", "Nombre", "CodigoEstandar", "PrecioUnitario", "UnidadMedida",
                "TratamientoIVA", "TarifaIVA", "CategoriaImpuestoAdicional", "TarifaImpuestoAdicional");

            sheet.Cell(2, 1).Value = "SKU-001";
            sheet.Cell(2, 2).Value = "Producto de ejemplo";
            sheet.Cell(2, 3).Value = "";
            sheet.Cell(2, 4).Value = 50000;
            sheet.Cell(2, 5).Value = "94";
            sheet.Cell(2, 6).Value = "Gravado";
            sheet.Cell(2, 7).Value = 19;
            sheet.Cell(2, 8).Value = "";
            sheet.Cell(2, 9).Value = "";

            ExcelTemplateHelper.AddDropdown(sheet, "F2:F1000", "Gravado", "Exento", "Excluido");

            sheet.Columns().AdjustToContents();
            var bytes = ExcelTemplateHelper.ToBytes(workbook);
            return File(bytes, ExcelTemplateHelper.XlsxContentType, "plantilla_productos.xlsx");
        }

        // Plantilla (fila 1 = encabezados, datos desde la fila 2):
        // Codigo | Nombre | CodigoEstandar | PrecioUnitario | UnidadMedida | TratamientoIVA (GRAVADO/EXENTO/EXCLUIDO) | TarifaIVA | CategoriaImpuestoAdicional | TarifaImpuestoAdicional
        [HttpPost("import")]
        public async Task<IActionResult> Import(IFormFile file)
        {
            try
            {
                var clientId = GetCurrentClientId();

                if (file == null || file.Length == 0)
                {
                    return BadRequest("Debes adjuntar un archivo Excel (.xlsx).");
                }

                var summary = new ImportSummary();
                using var stream = file.OpenReadStream();
                using var workbook = new XLWorkbook(stream);
                var sheet = workbook.Worksheets.First();
                var rows = sheet.RowsUsed().Skip(1);

                // Por código DIAN (columna "UnidadMedida" del Excel) — el archivo sigue trayendo
                // texto plano ("94", "KGM"), no el Id del catálogo, así que se resuelve aquí.
                var unitsByDianCode = await _dbContext.UnitsOfMeasure
                    .Where(u => u.IsActive)
                    .ToDictionaryAsync(u => u.DianCode, u => u.Id, StringComparer.OrdinalIgnoreCase);

                foreach (var row in rows)
                {
                    summary.TotalRows++;
                    var rowNumber = row.RowNumber();

                    try
                    {
                        var code = row.Cell(1).GetString().Trim();
                        var name = row.Cell(2).GetString().Trim();

                        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(name))
                        {
                            summary.Results.Add(new ImportRowResult { Row = rowNumber, Success = false, Message = "Código y nombre son obligatorios." });
                            summary.Failed++;
                            continue;
                        }

                        var scope = ParseScope(Request.Query["scope"]);

                        if (await _dbContext.Products.AnyAsync(p => p.ClientId == clientId && p.Scope == scope && p.Code == code))
                        {
                            summary.Results.Add(new ImportRowResult { Row = rowNumber, Success = false, Message = $"Ya existe un producto con código {code}." });
                            summary.Failed++;
                            continue;
                        }

                        if (!Enum.TryParse<IvaTreatment>(row.Cell(6).GetString().Trim(), true, out var ivaTreatment))
                        {
                            ivaTreatment = IvaTreatment.Gravado;
                        }

                        var product = new Product
                        {
                            Id = Guid.NewGuid(),
                            ClientId = clientId,
                            Scope = scope,
                            Code = code,
                            Name = name,
                            StandardCode = row.Cell(3).GetString().Trim(),
                            UnitPrice = row.Cell(4).GetValue<decimal>(),
                            UnitOfMeasureId = unitsByDianCode.TryGetValue(row.Cell(5).GetString().Trim(), out var uomId) ? uomId : Fel.Core.Entities.UnitOfMeasure.DefaultUnidadId,
                            IvaTreatment = scope == ProductScope.Support ? IvaTreatment.Exento : ivaTreatment,
                            IvaRate = scope == ProductScope.Support ? 0 : ivaTreatment == IvaTreatment.Gravado && !row.Cell(7).IsEmpty() ? row.Cell(7).GetValue<decimal>() : 0,
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        };

                        var taxCategory = scope == ProductScope.Support ? string.Empty : row.Cell(8).GetString().Trim();
                        var taxRateCell = row.Cell(9);
                        if (!string.IsNullOrWhiteSpace(taxCategory) && !taxRateCell.IsEmpty())
                        {
                            product.Taxes.Add(new ProductTax
                            {
                                Id = Guid.NewGuid(),
                                ProductId = product.Id,
                                TaxCategory = taxCategory.ToUpperInvariant(),
                                Rate = taxRateCell.GetValue<decimal>()
                            });
                        }

                        _dbContext.Products.Add(product);
                        summary.Results.Add(new ImportRowResult { Row = rowNumber, Success = true, Message = "Creado." });
                        summary.Succeeded++;
                    }
                    catch (Exception ex)
                    {
                        summary.Results.Add(new ImportRowResult { Row = rowNumber, Success = false, Message = ex.Message });
                        summary.Failed++;
                    }
                }

                await _dbContext.SaveChangesAsync();
                return Ok(summary);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(ex.Message);
            }
        }

        private static ProductScope ParseScope(string? value) =>
            !string.IsNullOrWhiteSpace(value) && Enum.TryParse<ProductScope>(value, true, out var scope)
                ? scope
                : ProductScope.Invoice;

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
