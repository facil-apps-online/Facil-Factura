using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Fel.Core.Entities;

namespace Fel.Api.Client.Imports
{
    public sealed class ProductImportContext
    {
        public Guid ClientId { get; init; }
        public ProductScope Scope { get; init; } = ProductScope.Invoice;
        public IReadOnlyList<UnitOfMeasure> Units { get; init; } = Array.Empty<UnitOfMeasure>();
        // Códigos que ya existen en la misma familia de productos del cliente.
        public ISet<string> ExistingCodes { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        // Tarifas de IVA del catálogo (19, 5, 10...): una tarifa distinta se carga igual, con aviso.
        public ISet<decimal> IvaRates { get; init; } = new HashSet<decimal>();
    }

    public sealed class ProductImportResult
    {
        public ImportPreview Preview { get; } = new();
        public List<Product> Products { get; } = new();
    }

    // Carga masiva de productos. Las columnas son las del Excel de inventario del cliente (REF, DESCRIPCION, CATEGORIA, PRECIO_DE_VENTA_UNITARIO,
    // PRECIO_INCLUYE_IMPUESTOS, UNIDAD, IVA%, IMP_CONSUMO%, IMP_LICOR, CONTROL_INVENTARIO) y se reconocen por nombre. La carga es fiel: lo que el archivo trae
    // se guarda tal cual y lo que no trae no se inventa; un producto sin IVA (vacío o en 0) ni impuesto al consumo queda marcado "impuestos por completar".
    public static class ProductImporter
    {
        public static readonly IReadOnlyDictionary<string, string[]> Aliases = new Dictionary<string, string[]>
        {
            ["REF"] = new[] { "CODIGO", "SKU", "REFERENCIA" },
            ["DESCRIPCION"] = new[] { "NOMBRE", "PRODUCTO" },
            ["CATEGORIA"] = Array.Empty<string>(),
            ["PRECIO_DE_VENTA_UNITARIO"] = new[] { "PRECIO_UNITARIO", "PRECIO", "VALOR_UNITARIO" },
            ["PRECIO_INCLUYE_IMPUESTOS"] = new[] { "PRECIO_INCLUYE_IVA", "INCLUYE_IVA", "INCLUYE_IMPUESTOS" },
            ["UNIDAD"] = new[] { "UNIDAD_MEDIDA", "UNIDAD_DE_MEDIDA" },
            ["IVA"] = new[] { "TARIFA_IVA", "IMPUESTO_IVA" },
            ["IMP_CONSUMO"] = new[] { "IMPOCONSUMO", "IMPUESTO_CONSUMO", "INC" },
            ["IMP_LICOR"] = new[] { "IMPUESTO_LICOR", "IMP_CONSUMO_LICOR" },
            ["CONTROL_INVENTARIO"] = Array.Empty<string>(),
            ["CODIGO_ESTANDAR"] = new[] { "UNSPSC" }
        };

        public static readonly string[] Required = { "REF", "DESCRIPCION" };

        public static ProductImportResult Analyze(SheetData sheet, ProductImportContext ctx)
        {
            var result = new ProductImportResult();
            result.Preview.FileErrors.AddRange(sheet.FileErrors);
            if (sheet.FileErrors.Count > 0) return result;

            var seen = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var isSupport = ctx.Scope == ProductScope.Support;

            foreach (var row in sheet.Rows)
            {
                var preview = new ImportRowPreview { Row = row.Row };
                result.Preview.Rows.Add(preview);

                var code = ImportText.Collapse(row.Get("REF"));
                var name = ImportText.Collapse(row.Get("DESCRIPCION"));
                if (code.Length == 0) preview.Errors.Add("Falta el código (REF).");
                if (name.Length == 0) preview.Errors.Add("Falta la descripción.");

                // ---- precio
                decimal price = 0;
                var priceText = row.Get("PRECIO_DE_VENTA_UNITARIO");
                if (priceText.Length == 0) preview.Warnings.Add("Sin precio; se cargó en 0.");
                else
                {
                    var parsed = ImportText.ParseDecimal(priceText);
                    if (parsed == null) preview.Errors.Add($"El precio '{priceText}' no es un número.");
                    else if (parsed < 0) preview.Errors.Add("El precio no puede ser negativo.");
                    else { price = parsed.Value; if (price == 0) preview.Warnings.Add("Precio en 0."); }
                }

                // ---- unidad de medida
                var unitId = UnitOfMeasure.DefaultUnidadId;
                var unitText = ImportText.Fold(row.Get("UNIDAD"));
                string unitLabel = "Unidad";
                // EA (Each, UN/CEFACT) es la "Unidad" de la DIAN (94): así la nombran la mayoría de los sistemas contables.
                if (unitText is "EA" or "UN" or "UND" or "UNIDAD" or "UNIDADES") unitText = string.Empty;
                if (unitText.Length > 0)
                {
                    var unit = ctx.Units.FirstOrDefault(u => ImportText.Fold(u.Abbreviation) == unitText || ImportText.Fold(u.DianCode) == unitText || ImportText.Fold(u.Name) == unitText);
                    if (unit != null) { unitId = unit.Id; unitLabel = unit.Name; }
                    else preview.Warnings.Add($"Unidad '{row.Get("UNIDAD")}' no reconocida; se usó Unidad.");
                }

                // ---- impuestos (fieles a lo que trae el archivo)
                var ivaText = row.Get("IVA");
                decimal iva = 0;
                var ivaParsed = ImportText.ParseDecimal(ivaText);
                if (ivaText.Length > 0 && ivaParsed == null) preview.Errors.Add($"La tarifa de IVA '{ivaText}' no es un número.");
                else if (ivaParsed is < 0 or > 100) preview.Errors.Add("La tarifa de IVA debe estar entre 0 y 100.");
                else iva = ivaParsed ?? 0;

                var consumoText = row.Get("IMP_CONSUMO");
                var consumoParsed = ImportText.ParseDecimal(consumoText);
                if (consumoText.Length > 0 && consumoParsed == null) preview.Errors.Add($"El impuesto al consumo '{consumoText}' no es un número.");
                var licorText = row.Get("IMP_LICOR");
                var licorParsed = ImportText.ParseDecimal(licorText);
                if (licorText.Length > 0 && licorParsed == null) preview.Errors.Add($"El impuesto al licor '{licorText}' no es un número.");
                var consumo = consumoParsed is > 0 ? consumoParsed.Value : 0;
                var licor = licorParsed is > 0 ? licorParsed.Value : 0;

                var includes = ImportText.ParseBool(row.Get("PRECIO_INCLUYE_IMPUESTOS")) ?? false;

                var ivaTreatment = IvaTreatment.Gravado;
                var taxesPending = false;
                string ivaLabel;
                if (isSupport)
                {
                    // En documento soporte los impuestos no aplican (ver ProductController).
                    ivaTreatment = IvaTreatment.Exento; iva = 0; consumo = 0; licor = 0; includes = false;
                    ivaLabel = "No aplica";
                    if (ivaText.Length > 0 || consumoText.Length > 0 || licorText.Length > 0) preview.Warnings.Add("En documento soporte los impuestos no aplican; se ignoraron.");
                }
                else if (iva > 0)
                {
                    ivaLabel = $"{iva.ToString("0.##", CultureInfo.InvariantCulture)} % · Gravado";
                    if (ctx.IvaRates.Count > 0 && !ctx.IvaRates.Contains(iva))
                        preview.Warnings.Add($"La tarifa de IVA {iva.ToString("0.##", CultureInfo.InvariantCulture)} % no está en el catálogo ({string.Join(", ", ctx.IvaRates.OrderByDescending(r => r))}).");
                }
                else
                {
                    // Sin IVA (vacío o en 0): no se inventa un tratamiento. Si tampoco trae impuesto al consumo, el producto queda pendiente de impuestos.
                    ivaTreatment = IvaTreatment.Excluido;
                    taxesPending = consumo == 0 && licor == 0;
                    ivaLabel = taxesPending ? "Por completar" : "Sin IVA";
                    if (taxesPending) preview.Notes.Add(ivaText.Length == 0 ? "Sin IVA en el archivo: queda como \"impuestos por completar\"." : "IVA en 0 en el archivo: queda como \"impuestos por completar\".");
                    if (includes && taxesPending) preview.Warnings.Add("El precio dice incluir impuestos pero no hay IVA definido; completa los impuestos del producto.");
                }

                // ---- estado
                if (preview.Errors.Count > 0) preview.Status = ImportStatus.Error;
                else if (ctx.ExistingCodes.Contains(code)) { preview.Status = ImportStatus.Duplicate; preview.Notes.Add($"Ya existe un producto con el código {code}."); }
                else if (seen.TryGetValue(code, out var firstRow)) { preview.Status = ImportStatus.Duplicate; preview.Notes.Add($"Código repetido en la fila {firstRow}."); }

                var extra = new List<string>();
                if (consumo > 0) extra.Add($"Consumo {consumo.ToString("0.##", CultureInfo.InvariantCulture)} %");
                if (licor > 0) extra.Add($"Licor {licor.ToString("0.##", CultureInfo.InvariantCulture)} %");
                preview.Values["Código"] = code;
                preview.Values["Nombre"] = name;
                preview.Values["Precio"] = price.ToString("N2", CultureInfo.InvariantCulture) + (includes ? " (con IVA)" : "");
                preview.Values["IVA"] = ivaLabel;
                preview.Values["Impuesto adicional"] = extra.Count == 0 ? null : string.Join(" · ", extra);
                preview.Values["Unidad"] = unitLabel;

                if (preview.Status != ImportStatus.Create) continue;
                seen[code] = row.Row;

                var product = new Product
                {
                    Id = Guid.NewGuid(),
                    ClientId = ctx.ClientId,
                    Scope = ctx.Scope,
                    Code = code,
                    Name = name,
                    StandardCode = ImportText.Collapse(row.Get("CODIGO_ESTANDAR")),
                    UnitPrice = price,
                    UnitOfMeasureId = unitId,
                    IvaTreatment = ivaTreatment,
                    IvaRate = ivaTreatment == IvaTreatment.Gravado ? iva : 0,
                    PriceIncludesIva = includes,
                    TaxesPending = taxesPending,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                if (consumo > 0) product.Taxes.Add(new ProductTax { Id = Guid.NewGuid(), ProductId = product.Id, TaxCategory = "IMP_CONSUMO", Rate = consumo });
                if (licor > 0) product.Taxes.Add(new ProductTax { Id = Guid.NewGuid(), ProductId = product.Id, TaxCategory = "IMP_CONSUMO_LICOR", Rate = licor });
                result.Products.Add(product);
            }

            result.Preview.Tally();
            return result;
        }
    }
}
