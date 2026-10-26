using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Fel.Core.Entities;

namespace Fel.Api.Client.Imports
{
    public sealed class InvoiceImportContext
    {
        public Guid ClientId { get; init; }
        public Guid BranchId { get; init; }
        public Guid DocumentTypeId { get; init; }
        public string DocumentTypeCode { get; init; } = "FE-STD";
        public Guid ResolutionId { get; init; }
        // Fecha de emisión de los borradores: hoy (hora de Colombia), sin importar la fecha del archivo.
        public DateTime Today { get; init; }
        public CustomerImportContext Customers { get; init; } = new();
        // Terceros que ya existen con rol Cliente: identificación normalizada -> id.
        public IReadOnlyDictionary<string, Guid> ExistingCustomerIds { get; init; } = new Dictionary<string, Guid>();
        public ProductImportContext Products { get; init; } = new();
        // Productos de factura que ya existen (con su unidad de medida e impuestos): código -> producto.
        public IReadOnlyDictionary<string, Product> ExistingProducts { get; init; } = new Dictionary<string, Product>();
        // Catálogos: categoría -> nombre.
        public IReadOnlyDictionary<string, string> PaymentMeans { get; init; } = new Dictionary<string, string>();
        public IReadOnlyDictionary<string, string> FormaPago { get; init; } = new Dictionary<string, string>();
        public BulkImportOptions Options => Customers.Options;
    }

    public sealed class InvoiceImportResult
    {
        public ImportPreview Preview { get; } = new();
        // Solo lo que usan las facturas que se van a crear.
        public List<Customer> Customers { get; } = new();
        public List<Product> Products { get; } = new();
        public List<Document> Documents { get; } = new();
    }

    // Carga masiva de facturas de venta como borradores. Cada fila del archivo es una línea y las filas con el mismo NUMERO forman una factura. Crea el tercero
    // (rol Cliente) y el producto cuando no existen, reutilizando CustomerImporter y ProductImporter para validarlos y para que el usuario decida lo mismo
    // que en sus cargas masivas. Lógica pura: el controlador entrega los catálogos y lo que ya existe.
    //  - La fecha de emisión es la de hoy; el consecutivo lo asigna el sistema al emitir (el NUMERO del archivo solo agrupa las líneas).
    //  - Contado: vence el día de la emisión. Crédito: se usa la fecha de vencimiento del archivo (debe ser posterior a la emisión).
    //  - Producto existente: la línea toma sus impuestos. Producto nuevo: el IVA del archivo si lo trae; si no, la línea es Excluida y el producto queda
    //    "impuestos por completar".
    public static class InvoiceImporter
    {
        public static readonly IReadOnlyDictionary<string, string[]> Aliases = new Dictionary<string, string[]>
        {
            ["FECHA_EXPEDICION"] = new[] { "FECHA_EMISION", "FECHA" },
            ["FECHA_VENCIMIENTO"] = new[] { "VENCIMIENTO" },
            ["NUMERO"] = new[] { "NUMERO_FACTURA", "FACTURA", "CONSECUTIVO" },
            ["MEDIO_DE_PAGO"] = new[] { "MEDIO_PAGO" },
            ["TIPO_MEDIO_DE_PAGO"] = new[] { "FORMA_DE_PAGO", "FORMA_PAGO", "TIPO_DE_PAGO" },
            ["ORDEN_DE_COMPRA"] = new[] { "ORDEN_COMPRA" },
            ["MONEDA"] = Array.Empty<string>(),
            ["CLIENTE_PAIS"] = Array.Empty<string>(),
            ["CLIENTE_NOMBRE"] = new[] { "CLIENTE_RAZON_SOCIAL" },
            ["OBSERVACIONES"] = new[] { "NOTAS" },
            ["CLIENTE_PRIMER_NOMBRE"] = Array.Empty<string>(),
            ["CLIENTE_APELLIDO"] = Array.Empty<string>(),
            ["CLIENTE_NOMBRE_COMERCIAL"] = Array.Empty<string>(),
            ["CLIENTE_IDENTIFICATION"] = new[] { "CLIENTE_IDENTIFICACION", "CLIENTE_DOCUMENTO", "CLIENTE_NIT" },
            ["CLIENTE_TIPO_IDENTIFICATION"] = new[] { "CLIENTE_TIPO_IDENTIFICACION", "CLIENTE_TIPO_DOCUMENTO" },
            ["CLIENTE_DIRECCION"] = Array.Empty<string>(),
            ["CLIENTE_TELEFONO"] = Array.Empty<string>(),
            ["CLIENTE_CIUDAD"] = Array.Empty<string>(),
            ["CLIENTE_DEPARTAMENTO"] = Array.Empty<string>(),
            ["CLIENTE_TIPO"] = Array.Empty<string>(),
            ["CLIENTE_CORREO"] = new[] { "CLIENTE_EMAIL" },
            ["CLIENTE_TAX_LEVEL"] = Array.Empty<string>(),
            ["CLIENTE_REGIMEN"] = Array.Empty<string>(),
            ["ITEM_DESCRIPCION"] = Array.Empty<string>(),
            ["ITEM_REFERENCIA"] = new[] { "ITEM_CODIGO" },
            ["ITEM_CANTIDAD"] = Array.Empty<string>(),
            ["ITEM_PRECIO"] = new[] { "ITEM_VALOR_UNITARIO" },
            ["ITEM_DESCUENTO"] = Array.Empty<string>(),
            ["IVA"] = Array.Empty<string>(),
            ["IMP_CONSUMO"] = Array.Empty<string>(),
            ["RET_IVA"] = Array.Empty<string>(),
            ["RET_ICA"] = Array.Empty<string>(),
            ["RET_FUENTE"] = Array.Empty<string>(),
            ["DESCUENTO_GLOBAL"] = Array.Empty<string>(),
            ["DESCUENTO_GLOBAL_DESCRIPCION"] = Array.Empty<string>()
        };

        public static readonly string[] Required = { "CLIENTE_IDENTIFICATION", "CLIENTE_TIPO_IDENTIFICATION", "ITEM_REFERENCIA", "ITEM_CANTIDAD", "ITEM_PRECIO", "TIPO_MEDIO_DE_PAGO" };

        private static readonly Dictionary<string, string> PaymentMeansByText = new()
        {
            ["BANK TRANSFER"] = "BANK_TRANSFER", ["TRANSFERENCIA"] = "BANK_TRANSFER", ["TRANSFERENCIA BANCARIA"] = "BANK_TRANSFER", ["CONSIGNACION"] = "BANK_TRANSFER",
            ["CASH"] = "CASH", ["EFECTIVO"] = "CASH",
            ["CREDIT CARD"] = "CREDIT_CARD", ["TARJETA CREDITO"] = "CREDIT_CARD", ["TARJETA DE CREDITO"] = "CREDIT_CARD",
            ["DEBIT CARD"] = "DEBIT_CARD", ["TARJETA DEBITO"] = "DEBIT_CARD", ["TARJETA DE DEBITO"] = "DEBIT_CARD",
            ["MUTUAL AGREEMENT"] = "MUTUAL_AGREEMENT", ["ACUERDO MUTUO"] = "MUTUAL_AGREEMENT"
        };

        private static readonly string[] DateFormats = { "dd/MM/yyyy", "d/M/yyyy", "yyyy-MM-dd", "dd-MM-yyyy", "d-M-yyyy", "yyyy/MM/dd" };

        private sealed class Group
        {
            public string Number = string.Empty;
            public List<SheetRow> Rows = new();
            public SheetRow First => Rows[0];
        }

        // Resultado del análisis de un tercero o de un producto nuevo (la fila de la factura donde apareció primero).
        private sealed class Analysis<T> where T : class
        {
            public ImportRowPreview Preview = null!;
            public T? Entity;
        }

        public static InvoiceImportResult Analyze(SheetData sheet, InvoiceImportContext ctx)
        {
            var result = new InvoiceImportResult();
            result.Preview.FileErrors.AddRange(sheet.FileErrors);
            if (sheet.FileErrors.Count > 0) return result;

            // ---- facturas = filas con el mismo NUMERO (sin número, cada fila es una factura)
            var groups = new List<Group>();
            var byNumber = new Dictionary<string, Group>(StringComparer.OrdinalIgnoreCase);
            foreach (var row in sheet.Rows)
            {
                var number = ImportText.Collapse(row.Get("NUMERO"));
                if (number.Length > 0 && byNumber.TryGetValue(number, out var existing)) { existing.Rows.Add(row); continue; }
                var g = new Group { Number = number };
                g.Rows.Add(row);
                groups.Add(g);
                if (number.Length > 0) byNumber[number] = g;
            }

            var customers = AnalyzeCustomers(groups, ctx);
            var products = AnalyzeProducts(groups, ctx);

            var usedCustomers = new HashSet<Guid>();
            var usedProducts = new HashSet<Guid>();

            foreach (var group in groups)
            {
                var preview = new ImportRowPreview { Row = group.First.Row };
                result.Preview.Rows.Add(preview);
                ctx.Options.Decisions.TryGetValue(preview.Row, out var decisions);
                decisions ??= new Dictionary<string, string>();
                var omit = false;
                ImportDecision? pending = null;
                var head = group.First;
                if (group.Rows.Count > 1) preview.Notes.Add($"Factura de {group.Rows.Count} líneas (filas {string.Join(", ", group.Rows.Select(r => r.Row))}).");

                // Mirar lo que dice el análisis de un tercero/producto: bloquea la factura con un error, una omisión o una decisión pendiente.
                bool Blocked<T>(Analysis<T>? a, string what) where T : class
                {
                    if (a == null) return false;
                    var p = a.Preview;
                    switch (p.Status)
                    {
                        case ImportStatus.Create: case ImportStatus.Duplicate: return false;
                        case ImportStatus.Error:
                            foreach (var e in p.Errors) preview.Errors.Add($"{what}: {e}");
                            return true;
                        case ImportStatus.Skipped: omit = true; return true;
                        default:
                            if (p.Row == preview.Row) pending ??= p.Decision;
                            else preview.Errors.Add($"{what} necesita una decisión en la fila {p.Row}.");
                            return true;
                    }
                }

                // ---- cliente
                var customerKey = CustomerKey(head);
                var customerLabel = ImportText.Collapse(head.Get("CLIENTE_NOMBRE"));
                if (customerLabel.Length == 0) customerLabel = ImportText.Collapse(head.Get("CLIENTE_PRIMER_NOMBRE") + " " + head.Get("CLIENTE_APELLIDO"));
                Guid? customerId = null;
                var customerState = "—";
                if (customerKey.Length == 0) preview.Errors.Add("Falta la identificación del cliente.");
                else
                {
                    if (group.Rows.Any(r => CustomerKey(r) != customerKey)) preview.Errors.Add("Las líneas de esta factura traen clientes distintos.");
                    if (ctx.ExistingCustomerIds.TryGetValue(customerKey, out var existingId)) { customerId = existingId; customerState = "Existe"; }
                    else if (customers.TryGetValue(customerKey, out var ca))
                    {
                        customerState = "Nuevo";
                        if (!Blocked(ca, "Cliente"))
                        {
                            customerId = ca.Entity?.Id;
                            if (ca.Preview.Row == preview.Row) foreach (var w in ca.Preview.Warnings) preview.Warnings.Add($"Cliente: {w}");
                            if (ca.Preview.Row == preview.Row) foreach (var n in ca.Preview.Notes) preview.Notes.Add($"Cliente: {n}");
                        }
                    }
                }
                var country = ImportText.Fold(head.Get("CLIENTE_PAIS"));
                if (country.Length > 0 && country != "CO" && country != "COLOMBIA") preview.Errors.Add($"El cliente es del país '{head.Get("CLIENTE_PAIS")}': esta carga solo emite facturas nacionales.");

                // ---- moneda
                var currency = ImportText.Fold(head.Get("MONEDA"));
                if (currency.Length > 0 && currency != "COP") preview.Errors.Add($"La moneda '{head.Get("MONEDA")}' no se admite: esta carga solo emite en COP.");

                // ---- forma y medio de pago
                var formaText = ImportText.Fold(head.Get("TIPO_MEDIO_DE_PAGO"));
                string? forma = formaText switch { "DEBITO" or "CONTADO" => "DEBITO", "CREDITO" => "CREDITO", _ => null };
                if (forma != null && !ctx.FormaPago.ContainsKey(forma)) forma = null;
                if (forma == null) preview.Errors.Add(formaText.Length == 0 ? "Falta la forma de pago (Contado o Crédito)." : $"La forma de pago '{head.Get("TIPO_MEDIO_DE_PAGO")}' no es válida (Contado/DEBITO o Crédito/CREDITO).");

                string? means = ResolvePaymentMeans(head.Get("MEDIO_DE_PAGO"), ctx.PaymentMeans);
                if (means == null && decisions.TryGetValue("paymentMeans", out var chosenMeans))
                {
                    if (chosenMeans == "omit") omit = true;
                    else if (ctx.PaymentMeans.ContainsKey(chosenMeans)) means = chosenMeans;
                }
                if (means == null && forma == "DEBITO" && !omit)
                {
                    var raw = head.Get("MEDIO_DE_PAGO");
                    pending ??= new ImportDecision
                    {
                        Field = "paymentMeans",
                        Label = raw.Length == 0 ? "La factura es de contado y no trae medio de pago" : $"Medio de pago '{raw}'",
                        Options = ctx.PaymentMeans.OrderBy(m => m.Value).Select(m => new ImportOption(m.Key, m.Value)).Append(new ImportOption("omit", "Omitir esta fila")).ToList()
                    };
                }
                if (forma == "CREDITO" && means == null) means = null; // el crédito no exige el medio de pago

                // ---- fechas: emisión = hoy; vencimiento según la forma de pago
                var today = ctx.Today.Date;
                int? termDays = null;
                var dueLabel = forma == "DEBITO" ? "De contado" : null;
                if (forma == "CREDITO")
                {
                    var dueText = head.Get("FECHA_VENCIMIENTO");
                    var due = ParseDate(dueText);
                    if (dueText.Length == 0) preview.Errors.Add("La factura es a crédito y no trae fecha de vencimiento.");
                    else if (due == null) preview.Errors.Add($"La fecha de vencimiento '{dueText}' no es válida (use dd/mm/aaaa).");
                    else if (due.Value.Date <= today) preview.Errors.Add($"La fecha de vencimiento ({due.Value:dd/MM/yyyy}) debe ser posterior a la emisión ({today:dd/MM/yyyy}).");
                    else { termDays = (int)(due.Value.Date - today).TotalDays; dueLabel = $"{due.Value:dd/MM/yyyy} ({termDays} días)"; }
                }

                // ---- descuento general
                decimal? generalDiscount = null;
                var gdText = FirstFilled(group, "DESCUENTO_GLOBAL");
                if (gdText.Length > 0)
                {
                    var gd = ImportText.ParseMoney(gdText);
                    if (gd == null || gd < 0) preview.Errors.Add($"El descuento global '{gdText}' no es un valor válido.");
                    else if (gd > 0) generalDiscount = gd;
                }

                // ---- líneas
                var items = new List<DocumentItem>();
                var newProductNames = new List<string>();
                var line = 0;
                foreach (var row in group.Rows)
                {
                    var tag = group.Rows.Count > 1 ? $"Fila {row.Row}: " : string.Empty;
                    var code = ImportText.Collapse(row.Get("ITEM_REFERENCIA"));
                    if (code.Length == 0) { preview.Errors.Add(tag + "Falta la referencia del ítem."); continue; }

                    var qty = ImportText.ParseDecimal(row.Get("ITEM_CANTIDAD"));
                    if (qty == null || qty <= 0) { preview.Errors.Add(tag + $"La cantidad '{row.Get("ITEM_CANTIDAD")}' no es válida."); continue; }
                    var priceText = row.Get("ITEM_PRECIO");
                    var filePrice = ImportText.ParseMoney(priceText);
                    if (priceText.Length > 0 && (filePrice == null || filePrice < 0)) { preview.Errors.Add(tag + $"El precio '{priceText}' no es válido."); continue; }
                    var discountText = row.Get("ITEM_DESCUENTO");
                    var discount = ImportText.ParseDecimal(discountText) ?? 0;
                    if (discountText.Length > 0 && ImportText.ParseDecimal(discountText) == null) { preview.Errors.Add(tag + $"El descuento '{discountText}' no es válido."); continue; }
                    if (discount is < 0 or > 100) { preview.Errors.Add(tag + "El descuento debe estar entre 0 y 100."); continue; }
                    var description = ImportText.Collapse(row.Get("ITEM_DESCRIPCION"));

                    // producto: el que ya existe o el que se crea con esta carga
                    Product? product;
                    var isNewProduct = false;
                    if (ctx.ExistingProducts.TryGetValue(code, out var existingProduct)) product = existingProduct;
                    else if (products.TryGetValue(code, out var pa))
                    {
                        isNewProduct = true;
                        if (Blocked(pa, $"Producto {code}")) continue;
                        product = pa.Entity;
                        if (pa.Preview.Row == preview.Row)
                        {
                            foreach (var n in pa.Preview.Notes) preview.Notes.Add($"Producto {code}: {n}");
                            foreach (var w in pa.Preview.Warnings) preview.Warnings.Add($"Producto {code}: {w}");
                        }
                        if (product == null) { preview.Errors.Add(tag + $"No se pudo preparar el producto {code}."); continue; }
                    }
                    else { preview.Errors.Add(tag + $"No se pudo preparar el producto {code}."); continue; }

                    var treatment = product.IvaTreatment;
                    var rate = treatment == IvaTreatment.Gravado ? product.IvaRate : 0m;
                    var fileIvaText = row.Get("IVA");
                    var fileIva = ImportText.ParseDecimal(fileIvaText);
                    if (fileIvaText.Length > 0 && fileIva == null) { preview.Errors.Add(tag + $"El IVA '{fileIvaText}' no es un número."); continue; }
                    if (!isNewProduct)
                    {
                        if (fileIva is > 0 && fileIva != rate)
                            preview.Warnings.Add(tag + $"El IVA del archivo ({fileIva.Value.ToString("0.##", CultureInfo.InvariantCulture)} %) no coincide con el del producto {code} ({rate.ToString("0.##", CultureInfo.InvariantCulture)} %); se usó el del producto.");
                        if (product.TaxesPending) preview.Warnings.Add(tag + $"El producto {code} tiene los impuestos por completar; la línea sale sin IVA.");
                    }
                    else newProductNames.Add(code);
                    decimal unitPrice;
                    if (filePrice != null) unitPrice = filePrice.Value;
                    else if (!isNewProduct && product.UnitPrice > 0)
                    {
                        unitPrice = product.PriceIncludesIva && treatment == IvaTreatment.Gravado ? Math.Round(product.UnitPrice / (1 + rate / 100), 4) : product.UnitPrice;
                        preview.Notes.Add(tag + $"Sin precio en el archivo: se usó el del producto {code}.");
                    }
                    else { preview.Errors.Add(tag + "Falta el precio del ítem."); continue; }

                    var unit = ctx.Products.Units.FirstOrDefault(u => u.Id == product.UnitOfMeasureId) ?? product.UnitOfMeasure;
                    var lineBase = Math.Round(qty.Value * unitPrice * (1 - discount / 100), 2, MidpointRounding.AwayFromZero);
                    var tax = Math.Round(lineBase * rate / 100, 2, MidpointRounding.AwayFromZero);
                    var additionalTaxes = product.Taxes
                        .Where(t => !string.IsNullOrWhiteSpace(t.TaxCategory) && t.Rate >= 0)
                        .Select(t => new DocumentItemTax
                        {
                            Id = Guid.NewGuid(),
                            TaxCategory = t.TaxCategory,
                            Rate = t.Rate,
                            BaseAmount = lineBase,
                            Amount = Math.Round(lineBase * t.Rate / 100, 2, MidpointRounding.AwayFromZero)
                        }).ToList();
                    var additionalTaxTotal = additionalTaxes.Sum(t => t.Amount);
                    var item = new DocumentItem
                    {
                        Id = Guid.NewGuid(),
                        ProductId = product.Id,
                        LineNumber = line++,
                        Code = product.Code,
                        Name = description.Length > 0 ? description : product.Name,
                        Quantity = qty.Value,
                        UnitPrice = unitPrice,
                        UnitOfMeasureCode = unit?.DianCode ?? "94",
                        UnitOfMeasureAbbreviation = unit?.Abbreviation ?? "EA",
                        UnitOfMeasureDisplayFormat = unit?.DisplayFormat ?? "Combined",
                        IvaTreatment = treatment,
                        TaxRate = rate,
                        DiscountRate = discount,
                        TaxAmount = tax,
                        TotalAmount = lineBase + tax + additionalTaxTotal,
                        AdditionalTaxes = additionalTaxes
                    };
                    foreach (var (column, category) in new[] { ("RET_FUENTE", "RET_FUENTE"), ("RET_ICA", "RET_ICA"), ("RET_IVA", "RET_IVA") })
                    {
                        var text = row.Get(column);
                        if (text.Length == 0) continue;
                        var retRate = ImportText.ParseDecimal(text);
                        if (retRate is null or < 0 or > 100) { preview.Errors.Add(tag + $"La retención {column} '{text}' no es válida."); continue; }
                        if (retRate == 0) continue;
                        var retBase = category == "RET_IVA" ? tax : lineBase;
                        item.Retentions.Add(new DocumentRetention
                        {
                            Id = Guid.NewGuid(), DocumentItemId = item.Id, TaxCategory = category, Rate = retRate.Value,
                            BaseAmount = retBase, Amount = Math.Round(retBase * retRate.Value / 100, 2, MidpointRounding.AwayFromZero)
                        });
                    }
                    items.Add(item);
                }
                if (items.Count == 0 && preview.Errors.Count == 0 && !omit && pending == null) preview.Errors.Add("La factura no tiene líneas.");

                var subtotal = items.Sum(i => i.TotalAmount - i.TaxAmount - i.AdditionalTaxes.Sum(t => t.Amount));
                var taxTotal = items.Sum(i => i.TaxAmount);
                var total = subtotal + taxTotal + items.Sum(i => i.AdditionalTaxes.Sum(t => t.Amount));
                if (generalDiscount > subtotal) preview.Errors.Add("El descuento global es mayor que el subtotal de la factura.");

                // ---- estado
                if (preview.Errors.Count > 0) preview.Status = ImportStatus.Error;
                else if (omit) preview.Status = ImportStatus.Skipped;
                else if (pending != null) { preview.Status = ImportStatus.Decision; preview.Decision = pending; }

                preview.Values["Factura"] = group.Number.Length > 0 ? group.Number : null;
                preview.Values["Cliente"] = customerLabel.Length > 0 ? customerLabel : null;
                preview.Values["Documento"] = $"{ImportText.Collapse(head.Get("CLIENTE_TIPO_IDENTIFICATION"))} {ImportText.Collapse(head.Get("CLIENTE_IDENTIFICATION"))}".Trim();
                preview.Values["Tercero"] = customerState;
                preview.Values["Producto"] = string.Join(", ", group.Rows.Select(r => ImportText.Collapse(r.Get("ITEM_REFERENCIA"))).Where(c => c.Length > 0).Distinct()) + (newProductNames.Count > 0 ? " (nuevo)" : "");
                preview.Values["Total"] = (total - (generalDiscount ?? 0)).ToString("N2", CultureInfo.InvariantCulture);
                preview.Values["Pago"] = forma == null ? null : forma == "DEBITO" ? $"Contado · {(means != null && ctx.PaymentMeans.TryGetValue(means, out var meansName) ? meansName : "sin medio")}" : "Crédito";
                preview.Values["Vencimiento"] = dueLabel;

                if (preview.Status != ImportStatus.Create || customerId == null) continue;

                var document = new Document
                {
                    Id = Guid.NewGuid(),
                    ClientId = ctx.ClientId,
                    BranchId = ctx.BranchId,
                    CustomerId = customerId,
                    DocumentTypeId = ctx.DocumentTypeId,
                    TypeCode = ctx.DocumentTypeCode,
                    ResolutionId = ctx.ResolutionId,
                    Status = "DRAFT",
                    CreatedAt = DateTime.UtcNow,
                    IssueDate = ctx.Today,
                    Currency = "COP",
                    PaymentMeansType = forma,
                    PaymentMeans = forma == "DEBITO" ? means : null,
                    PaymentTermDays = termDays,
                    PurchaseOrderReference = NullIfEmpty(FirstFilled(group, "ORDEN_DE_COMPRA")),
                    Notes = ImportText.Collapse(FirstFilled(group, "OBSERVACIONES")),
                    GeneralDiscountAmount = generalDiscount,
                    GeneralDiscountReason = generalDiscount == null ? null : NullIfEmpty(FirstFilled(group, "DESCUENTO_GLOBAL_DESCRIPCION")) ?? "Descuento",
                    Subtotal = subtotal,
                    TaxAmount = taxTotal,
                    TotalAmount = total
                };
                foreach (var item in items)
                {
                    item.DocumentId = document.Id;
                    document.Items.Add(item);
                    if (item.ProductId != null) usedProducts.Add(item.ProductId.Value);
                }
                usedCustomers.Add(customerId.Value);
                result.Documents.Add(document);
            }

            result.Customers.AddRange(customers.Values.Select(a => a.Entity).Where(c => c != null && usedCustomers.Contains(c.Id))!);
            result.Products.AddRange(products.Values.Select(a => a.Entity).Where(p => p != null && usedProducts.Contains(p.Id))!);
            result.Preview.Tally();
            return result;
        }

        // ---- terceros nuevos: una sola pasada de CustomerImporter con un renglón por cliente distinto (el de su primera factura)
        private static Dictionary<string, Analysis<Customer>> AnalyzeCustomers(List<Group> groups, InvoiceImportContext ctx)
        {
            var map = new Dictionary<string, Analysis<Customer>>();
            var sheet = new SheetData();
            var keys = new List<string>();
            foreach (var group in groups)
            {
                var head = group.First;
                var key = CustomerKey(head);
                if (key.Length == 0 || ctx.ExistingCustomerIds.ContainsKey(key) || keys.Contains(key)) continue;
                keys.Add(key);

                var first = ImportText.Collapse(head.Get("CLIENTE_PRIMER_NOMBRE"));
                var last = ImportText.Collapse(head.Get("CLIENTE_APELLIDO"));
                var splitName = first.Length > 0 && last.Length > 0; // si vienen nombres y apellidos por separado se arma el nombre con ellos
                var taxLevel = ImportText.Fold(head.Get("CLIENTE_TAX_LEVEL"));
                var personType = ImportText.Fold(head.Get("CLIENTE_TIPO"));
                sheet.Rows.Add(new SheetRow(head.Row, new Dictionary<string, string>
                {
                    ["IDENTIFICACION"] = head.Get("CLIENTE_IDENTIFICATION"),
                    ["TIPO_IDENTIFICACION"] = head.Get("CLIENTE_TIPO_IDENTIFICATION"),
                    ["NOMBRE"] = splitName ? first : string.Empty,
                    ["APELLIDO"] = splitName ? last : string.Empty,
                    ["RAZON_SOCIAL"] = head.Get("CLIENTE_NOMBRE"),
                    ["TIPO"] = personType.Contains("NATURAL") ? "NATURAL" : personType.Contains("JURIDICA") ? "JURIDICA" : string.Empty,
                    ["RESPONSABLE_IVA"] = taxLevel.Length == 0 ? string.Empty : taxLevel.StartsWith("NO ") ? "FALSE" : taxLevel.Contains("RESPONSABLE") ? "TRUE" : string.Empty,
                    ["REGIMEN"] = head.Get("CLIENTE_REGIMEN"),
                    ["CORREO"] = head.Get("CLIENTE_CORREO"),
                    ["TELEFONO"] = head.Get("CLIENTE_TELEFONO"),
                    ["DEPARTAMENTO"] = head.Get("CLIENTE_DEPARTAMENTO"),
                    ["CIUDAD"] = head.Get("CLIENTE_CIUDAD"),
                    ["DIRECCION"] = head.Get("CLIENTE_DIRECCION"),
                    ["CATEGORIA"] = "CLIENTE"
                }));
            }
            if (sheet.Rows.Count == 0) return map;

            var analysis = CustomerImporter.Analyze(sheet, ctx.Customers);
            var created = analysis.Customers.GetEnumerator();
            for (var i = 0; i < keys.Count; i++)
            {
                var preview = analysis.Preview.Rows[i];
                Customer? entity = null;
                if (preview.Status == ImportStatus.Create && created.MoveNext()) entity = created.Current;
                map[keys[i]] = new Analysis<Customer> { Preview = preview, Entity = entity };
            }
            return map;
        }

        // ---- productos nuevos: un renglón por código que no existe (el de su primera factura)
        private static Dictionary<string, Analysis<Product>> AnalyzeProducts(List<Group> groups, InvoiceImportContext ctx)
        {
            var map = new Dictionary<string, Analysis<Product>>(StringComparer.OrdinalIgnoreCase);
            var sheet = new SheetData();
            var codes = new List<string>();
            foreach (var group in groups)
                foreach (var row in group.Rows)
                {
                    var code = ImportText.Collapse(row.Get("ITEM_REFERENCIA"));
                    if (code.Length == 0 || ctx.ExistingProducts.ContainsKey(code) || codes.Contains(code, StringComparer.OrdinalIgnoreCase)) continue;
                    codes.Add(code);
                    var description = ImportText.Collapse(row.Get("ITEM_DESCRIPCION"));
                    var price = ImportText.ParseMoney(row.Get("ITEM_PRECIO"));
                    sheet.Rows.Add(new SheetRow(group.First.Row, new Dictionary<string, string>
                    {
                        ["REF"] = code,
                        ["DESCRIPCION"] = description.Length > 0 ? description : code,
                        ["PRECIO_DE_VENTA_UNITARIO"] = price?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                        ["IVA"] = row.Get("IVA"),
                        ["IMP_CONSUMO"] = row.Get("IMP_CONSUMO")
                    }));
                }
            if (sheet.Rows.Count == 0) return map;

            var analysis = ProductImporter.Analyze(sheet, ctx.Products);
            var created = analysis.Products.GetEnumerator();
            for (var i = 0; i < codes.Count; i++)
            {
                var preview = analysis.Preview.Rows[i];
                Product? entity = null;
                if (preview.Status == ImportStatus.Create && created.MoveNext()) entity = created.Current;
                map[codes[i]] = new Analysis<Product> { Preview = preview, Entity = entity };
            }
            return map;
        }

        // Identificación comparable: sin puntos, espacios ni dígito de verificación de un NIT ("900.123.456-7" -> "900123456").
        public static string NormalizeParty(string? number) => Regex.Replace(number ?? string.Empty, "[^A-Za-z0-9]", "").ToUpperInvariant();

        private static string CustomerKey(SheetRow row)
        {
            var raw = row.Get("CLIENTE_IDENTIFICATION").Trim();
            var type = ImportText.Fold(row.Get("CLIENTE_TIPO_IDENTIFICATION"));
            var dash = raw.LastIndexOf('-');
            if (dash > 0 && type is "NIT" or "31") raw = raw[..dash];
            return NormalizeParty(raw);
        }

        private static string? ResolvePaymentMeans(string raw, IReadOnlyDictionary<string, string> catalog)
        {
            var folded = ImportText.Fold(raw);
            if (folded.Length == 0) return null;
            if (PaymentMeansByText.TryGetValue(folded, out var category) && catalog.ContainsKey(category)) return category;
            foreach (var (key, name) in catalog)
                if (ImportText.Fold(key) == folded || ImportText.Fold(name) == folded) return key;
            return null;
        }

        private static DateTime? ParseDate(string? text) =>
            DateTime.TryParseExact((text ?? string.Empty).Trim(), DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;

        // Datos de la factura que el archivo repite o trae solo en una de sus líneas: se toma la primera que lo tenga.
        private static string FirstFilled(Group group, string column) =>
            group.Rows.Select(r => r.Get(column)).FirstOrDefault(v => v.Length > 0) ?? string.Empty;

        private static string? NullIfEmpty(string? value)
        {
            var v = ImportText.Collapse(value);
            return v.Length == 0 ? null : v;
        }
    }
}
