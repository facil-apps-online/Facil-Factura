using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Fel.Core.Entities;
using Fel.Core.Models;
using Fel.Infrastructure.Data;
using Fel.Infrastructure.Dataico;
using Fel.Infrastructure.Dataico.Models;
using Fel.Infrastructure.Dian;
using Fel.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace Fel.Api.Integration.Services
{
    // Error de la API de integración: código estable para programar contra él y mensaje para leer. Details lista cada campo con su problema.
    public sealed record ApiError(int Status, string Code, string Message, IReadOnlyList<string>? Details = null);

    public sealed class InvoiceBuildResult
    {
        public Document? Document { get; init; }
        public ApiError? Error { get; init; }
        // Avisos de lo que se ajustó o se omitió sin impedir la emisión (p. ej. una dirección sin municipio).
        public List<string> Warnings { get; } = new();
        // El prefijo y número ya existen y el documento ya no se puede editar (emitido o en proceso): se devuelve tal cual está.
        public Document? ExistingLocked { get; init; }
        // El número ya existía como borrador o rechazado y se reutilizó para corregirlo y reintentar.
        public bool Reused { get; init; }
    }

    // Convierte el JSON simple de factura (la misma estructura que recibe Dataico) en el Document que el portal también crea. A partir de ahí la
    // emisión es la de siempre (InvoicePublishingService), así el documento sale igual y se ve en el portal venga de donde venga.
    //
    // El emisor no viaja en el JSON: sale de la llave de API. La resolución sí: el integrador lleva su propio consecutivo, que puede llegar en
    // desorden, y aquí solo se exige que esté dentro del rango autorizado y que no esté usado.
    public class SimpleInvoiceFactory
    {
        public const string StandardInvoice = "FE-STD";
        public const string MandateInvoice = "FE-MANDATO";
        public const string ExportInvoice = "FE-EXP";
        public const string AiuInvoice = "FE-AIU";
        public const string PosDocument = "DE-POS";

        private readonly FelDbContext _db;

        public SimpleInvoiceFactory(FelDbContext db)
        {
            _db = db;
        }

        public async Task<InvoiceBuildResult> BuildAsync(DataicoInvoiceRequest request, Client client, Guid branchId)
        {
            // POS: sin adquirente identificado el tiquete va a nombre del consumidor final.
            if (IsPos(request) && string.IsNullOrWhiteSpace(request.customer?.party_identification))
                request.customer = new DataicoParty { party_identification_type = "13", party_identification = "222222222222", company_name = "Consumidor final" };

            var problems = Validate(request);
            if (problems.Count > 0)
                return Fail(400, "VALIDATION_ERROR", "El documento tiene datos inválidos o faltantes. Revisa el detalle de cada campo.", problems);

            var isMandate = string.Equals(request.operation, "MANDATO", StringComparison.OrdinalIgnoreCase);
            var isExport = string.Equals(request.invoice_type_code, "FACTURA_EXPORTACION", StringComparison.OrdinalIgnoreCase);
            var isAiu = IsAiu(request);
            var isPos = IsPos(request);
            if ((isAiu || isPos) && (isExport || isMandate))
                return Fail(400, "VALIDATION_ERROR", "Este tipo de documento no puede ser a la vez de exportación o de mandato.");

            var typeCode = isPos ? PosDocument : isExport ? ExportInvoice : isMandate ? MandateInvoice : isAiu ? AiuInvoice : StandardInvoice;
            var documentType = await _db.DocumentTypes.AsNoTracking().FirstOrDefaultAsync(t => t.Code == typeCode && t.IsActive);
            if (documentType == null)
                return Fail(422, "DOCUMENT_TYPE_UNAVAILABLE", $"El tipo de documento {typeCode} no está disponible.");

            var number = long.Parse(request.number.Trim(), CultureInfo.InvariantCulture);
            var prefix = (request.numbering.prefix ?? string.Empty).Trim();

            var resolutionResult = await FindResolutionAsync(client.Id, branchId, request.numbering, prefix, number, isPos ? PosDocument : "FE");
            if (resolutionResult.Error != null) return new InvoiceBuildResult { Error = resolutionResult.Error };
            var resolution = resolutionResult.Resolution!;

            var numberText = number.ToString(CultureInfo.InvariantCulture);
            var existing = await _db.Documents
                .Include(d => d.Items).ThenInclude(i => i.Retentions)
                .Include(d => d.Items).ThenInclude(i => i.AdditionalTaxes)
                .Include(d => d.GeneralRetentions)
                .Include(d => d.Observations)
                .FirstOrDefaultAsync(d => d.ClientId == client.Id && d.Prefix == resolution.Prefix && d.Number == numberText && d.ReferenceDocumentId == null);
            if (existing != null && !InvoicePublishingService.IsEditable(existing.Status))
                return new InvoiceBuildResult { ExistingLocked = existing };

            var warnings = new List<string>();
            var customerResult = await EnsureCustomerAsync(client.Id, request.customer, warnings);
            if (customerResult.Error != null) return new InvoiceBuildResult { Error = customerResult.Error };
            var customer = customerResult.Customer!;

            var itemsResult = isAiu ? await BuildAiuItemsAsync(request.aiu!, warnings) : await BuildItemsAsync(request.items, isMandate, warnings);
            if (itemsResult.Error != null) return new InvoiceBuildResult { Error = itemsResult.Error };

            var paymentMeansResult = await ResolvePaymentMeansAsync(request);
            if (paymentMeansResult.Error != null) return new InvoiceBuildResult { Error = paymentMeansResult.Error };

            var document = existing ?? new Document { Id = Guid.NewGuid(), ClientId = client.Id, CreatedAt = DateTime.UtcNow };
            if (existing != null)
            {
                // Se reemplaza todo el contenido: el integrador corrige el mismo número y reintenta sobre el mismo documento.
                _db.DocumentItems.RemoveRange(existing.Items);
                existing.Items.Clear();
                _db.DocumentGeneralRetentions.RemoveRange(existing.GeneralRetentions);
                existing.GeneralRetentions.Clear();
                _db.DocumentObservations.RemoveRange(existing.Observations);
                existing.Observations.Clear();
            }

            var issueDate = ParseDate(request.issue_date) ?? ColombiaTime.Now;
            document.TrackingId = document.Id.ToString();
            document.BranchId = branchId;
            document.Status = "DRAFT";
            document.TypeCode = typeCode;
            document.DocumentTypeId = documentType.Id;
            document.Prefix = resolution.Prefix;
            document.Number = numberText;
            document.ResolutionId = resolution.Id;
            document.CustomerId = customer.Id;
            document.Customer = customer;
            document.IssueDate = issueDate;
            document.PaymentMeansType = request.payment_means_type.Trim().ToUpperInvariant();
            document.PaymentMeans = paymentMeansResult.Category;
            document.PurchaseOrderReference = string.IsNullOrWhiteSpace(request.order_reference) ? null : request.order_reference.Trim();
            document.SectorExtensionData = isAiu ? AiuExtensionData(request.aiu!, itemsResult.Items) : isPos ? PosExtensionData(request.pos!) : "{}";
            var dueDate = ParseDate(request.payment_date);
            document.PaymentTermDays = dueDate.HasValue ? Math.Max(0, (int)(dueDate.Value.Date - issueDate.Date).TotalDays) : null;

            var notes = (request.notes ?? new List<string>()).Where(n => !string.IsNullOrWhiteSpace(n)).Select(n => n.Trim()).ToList();
            if (isMandate)
            {
                document.Notes = string.Empty;
                for (var i = 0; i < notes.Count; i++)
                    document.Observations.Add(new DocumentObservation { Id = Guid.NewGuid(), DocumentId = document.Id, Position = i + 1, Text = notes[i] });
            }
            else
            {
                document.Notes = string.Join(" ", notes);
            }

            var currencyError = await ApplyCurrencyAsync(document, request, client);
            if (currencyError != null) return new InvoiceBuildResult { Error = currencyError };

            ApplyCharges(document, request.charges);

            var line = 0;
            foreach (var item in itemsResult.Items)
            {
                item.DocumentId = document.Id;
                item.LineNumber = line++;
                document.Items.Add(item);
            }

            foreach (var retention in request.retentions ?? new List<DataicoTax>())
            {
                if (retention.tax_rate is not > 0) continue;
                document.GeneralRetentions.Add(new DocumentGeneralRetention
                {
                    Id = Guid.NewGuid(),
                    DocumentId = document.Id,
                    TaxCategory = retention.tax_category.Trim().ToUpperInvariant(),
                    Rate = retention.tax_rate.Value
                });
            }

            // El servidor calcula los totales con las mismas fórmulas del portal; el integrador no los envía.
            DocumentTotalsCalculator.Apply(document, document.Items);

            // En un documento ya guardado, las filas nuevas traen su Id y EF las tomaría como existentes (UPDATE sobre filas que no están);
            // se marcan como nuevas para que se inserten.
            if (existing != null)
            {
                foreach (var item in document.Items)
                {
                    _db.Entry(item).State = EntityState.Added;
                    foreach (var retention in item.Retentions) _db.Entry(retention).State = EntityState.Added;
                }
                foreach (var retention in document.GeneralRetentions) _db.Entry(retention).State = EntityState.Added;
                foreach (var observation in document.Observations) _db.Entry(observation).State = EntityState.Added;
            }

            return new InvoiceBuildResult { Document = document, Reused = existing != null }.WithWarnings(warnings);
        }

        private static bool IsPos(DataicoInvoiceRequest r) => string.Equals(r.operation, "POS", StringComparison.OrdinalIgnoreCase);

        private static string PosExtensionData(DataicoPosData x) =>
            System.Text.Json.JsonSerializer.Serialize(new
            {
                pos = new PosSaleData
                {
                    BoxPlate = x.box_plate.Trim(), BoxLocation = x.box_location.Trim(), Cashier = x.cashier.Trim(), BoxType = x.box_type.Trim(), SaleCode = x.sale_code.Trim(),
                    BuyerCode = string.IsNullOrWhiteSpace(x.buyer_code) ? null : x.buyer_code.Trim(),
                    BuyerName = string.IsNullOrWhiteSpace(x.buyer_name) ? null : x.buyer_name.Trim(),
                    BuyerPoints = x.buyer_points
                }
            }, new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase });

        private static bool IsAiu(DataicoInvoiceRequest r) => string.Equals(r.operation, "AIU", StringComparison.OrdinalIgnoreCase);

        private static AiuContractData ToContract(DataicoAiuContract a, decimal baseAmount) => new()
        {
            BaseAmount = baseAmount,
            AdminPercent = a.administration_percent ?? 10,
            UnforeseenPercent = a.unforeseen_percent ?? 5,
            ProfitPercent = a.utility_percent ?? 5,
            IvaRate = a.utility_iva_rate ?? 19,
            ServiceIvaRate = a.service_iva_rate ?? 0,
            ContractObject = (a.contract_object ?? string.Empty).Trim()
        };

        // Base de las líneas de servicio (antes de IVA): lo que se factura por el contrato, sobre lo que se calculan A, I y U.
        private static decimal ServiceBase(IEnumerable<DocumentItem> serviceLines) =>
            Math.Round(serviceLines.Sum(i => i.Quantity * i.UnitPrice * (1 - i.DiscountRate / 100m)), 2, MidpointRounding.AwayFromZero);

        // Mismo JSON que guarda el portal ({ aiu: {...} } en camelCase): el motor y las notas lo leen igual venga de donde venga.
        private static string AiuExtensionData(DataicoAiuContract a, IEnumerable<DocumentItem> lines) =>
            System.Text.Json.JsonSerializer.Serialize(new { aiu = ToContract(a, ServiceBase(lines.Where(i => AiuContractData.IsServiceLine(i.Code)))) },
                new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase });

        // Las líneas de la AIU: las de servicio y, después, Administración, Imprevistos y Utilidad como porcentaje de la base. Sin aiu.items se arma
        // una sola línea de servicio por base_amount (como el formulario antiguo); con aiu.items las líneas del integrador son las de servicio y la
        // base es su suma. A e I no causan IVA; la Utilidad usa su tarifa y cada línea de servicio la suya (sin impuestos, excluida).
        private async Task<(List<DocumentItem> Items, ApiError? Error)> BuildAiuItemsAsync(DataicoAiuContract source, List<string> warnings)
        {
            List<DocumentItem> service;
            if (source.items is { Count: > 0 })
            {
                var built = await BuildItemsAsync(source.items, false, warnings, "aiu.items");
                if (built.Error != null) return built;
                service = built.Items;
            }
            else
            {
                var contract = ToContract(source, source.base_amount);
                service = new List<DocumentItem> { AiuLine(AiuContractData.ServiceCode, string.IsNullOrWhiteSpace(contract.ContractObject) ? "Servicio contratado" : contract.ContractObject, contract.BaseAmount, contract.ServiceIvaRate) };
            }

            var aiu = ToContract(source, ServiceBase(service));
            decimal PercentOf(decimal percent) => Math.Round(aiu.BaseAmount * percent / 100, 2, MidpointRounding.AwayFromZero);
            service.Add(AiuLine(AiuContractData.AdminCode, "Administración (A.I.U.)", PercentOf(aiu.AdminPercent), 0));
            service.Add(AiuLine(AiuContractData.UnforeseenCode, "Imprevistos (A.I.U.)", PercentOf(aiu.UnforeseenPercent), 0));
            service.Add(AiuLine(AiuContractData.ProfitCode, "Utilidad (A.I.U.)", PercentOf(aiu.ProfitPercent), aiu.IvaRate));
            return (service, null);
        }

        private static DocumentItem AiuLine(string code, string name, decimal price, decimal ivaRate) => new()
        {
            Id = Guid.NewGuid(), Code = code, Name = name, Quantity = 1, UnitPrice = price,
            UnitOfMeasureCode = "94", UnitOfMeasureAbbreviation = "EA", UnitOfMeasureDisplayFormat = "Combined",
            IvaTreatment = ivaRate > 0 ? IvaTreatment.Gravado : IvaTreatment.Excluido, TaxRate = ivaRate
        };

        private static InvoiceBuildResult Fail(int status, string code, string message, IReadOnlyList<string>? details = null) =>
            new() { Error = new ApiError(status, code, message, details) };

        // ---------------------------------------------------------------------------------------------------------------------------
        // Validación de forma: lo que se puede decir sin tocar la base de datos. Cada problema nombra el campo para que el integrador lo ubique.
        // ---------------------------------------------------------------------------------------------------------------------------
        private static List<string> Validate(DataicoInvoiceRequest r)
        {
            var p = new List<string>();

            if (string.IsNullOrWhiteSpace(r.number)) p.Add("number: es obligatorio; es tu consecutivo sin prefijo (por ejemplo \"1001\").");
            else if (!long.TryParse(r.number.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var n) || n < 1)
                p.Add($"number: \"{r.number}\" no es válido; debe ser un entero positivo sin prefijo ni separadores.");

            if (r.numbering == null) p.Add("numbering: es obligatorio e indica el prefijo (y el número) de la resolución.");

            if (!string.IsNullOrWhiteSpace(r.issue_date) && ParseDate(r.issue_date) == null)
                p.Add($"issue_date: \"{r.issue_date}\" no es una fecha válida; usa dd/MM/yyyy.");
            if (!string.IsNullOrWhiteSpace(r.payment_date) && ParseDate(r.payment_date) == null)
                p.Add($"payment_date: \"{r.payment_date}\" no es una fecha válida; usa dd/MM/yyyy.");

            var type = (r.payment_means_type ?? string.Empty).Trim().ToUpperInvariant();
            if (type != "DEBITO" && type != "CREDITO") p.Add("payment_means_type: debe ser DEBITO (contado) o CREDITO.");
            else if (type == "DEBITO" && string.IsNullOrWhiteSpace(r.payment_means)) p.Add("payment_means: es obligatorio cuando payment_means_type es DEBITO (por ejemplo CASH para efectivo o DEBIT_CARD para tarjeta débito).");
            else if (type == "CREDITO" && ParseDate(r.payment_date) == null) p.Add("payment_date: es obligatorio cuando payment_means_type es CREDITO; es la fecha de vencimiento.");

            if (!string.IsNullOrWhiteSpace(r.invoice_type_code) && !r.invoice_type_code.Trim().ToUpperInvariant().StartsWith("FACTURA_"))
                p.Add($"invoice_type_code: \"{r.invoice_type_code}\" no está disponible en este endpoint; usa FACTURA_VENTA o FACTURA_EXPORTACION.");

            var c = r.customer;
            if (c == null) p.Add("customer: es obligatorio.");
            else
            {
                if (string.IsNullOrWhiteSpace(c.party_identification_type)) p.Add("customer.party_identification_type: es obligatorio (por ejemplo NIT o CC).");
                if (string.IsNullOrWhiteSpace(c.party_identification)) p.Add("customer.party_identification: es obligatorio.");
                if (string.IsNullOrWhiteSpace(c.company_name) && string.IsNullOrWhiteSpace(c.first_name) && string.IsNullOrWhiteSpace(c.family_name))
                    p.Add("customer: falta el nombre; envía company_name, o first_name y family_name.");
            }

            if (IsPos(r))
            {
                var x = r.pos;
                if (x == null) p.Add("pos: es obligatorio en el documento equivalente POS; indica la placa, ubicación, cajero, tipo de caja y código de venta.");
                else
                {
                    void Required(string name, string? value)
                    {
                        if (string.IsNullOrWhiteSpace(value)) p.Add($"pos.{name}: es obligatorio.");
                        else if (value.Trim().Length > PosSaleData.MaxLength) p.Add($"pos.{name}: no puede superar {PosSaleData.MaxLength} caracteres.");
                    }
                    Required("box_plate", x.box_plate); Required("box_location", x.box_location); Required("cashier", x.cashier);
                    Required("box_type", x.box_type); Required("sale_code", x.sale_code);
                    if (x.buyer_points is < 0) p.Add("pos.buyer_points: no puede ser negativo.");
                    var benefits = !string.IsNullOrWhiteSpace(x.buyer_code) || !string.IsNullOrWhiteSpace(x.buyer_name) || x.buyer_points.HasValue;
                    if (benefits && (string.IsNullOrWhiteSpace(x.buyer_code) || string.IsNullOrWhiteSpace(x.buyer_name) || !x.buyer_points.HasValue))
                        p.Add("pos: los beneficios del comprador van completos o no van; envía buyer_code, buyer_name y buyer_points juntos.");
                }
                if (r.items == null || r.items.Count == 0) p.Add("items: debe traer al menos una línea.");
            }
            else if (IsAiu(r))
            {
                var a = r.aiu;
                if (a == null) p.Add("aiu: es obligatorio en una factura AIU; indica la base (base_amount, o las líneas del servicio en aiu.items); contract_object es opcional.");
                else
                {
                    if (a.items is { Count: > 0 })
                    {
                        ValidateItemLines(a.items, "aiu.items", p);
                        if (a.base_amount > 0) p.Add("aiu.base_amount: no se envía junto con aiu.items; la base es la suma de las líneas.");
                    }
                    else if (a.base_amount <= 0) p.Add("aiu.base_amount: debe ser mayor que 0 (el valor del servicio contratado), o envía las líneas del servicio en aiu.items.");
                    foreach (var (name, value) in new[] { ("administration_percent", a.administration_percent), ("unforeseen_percent", a.unforeseen_percent), ("utility_percent", a.utility_percent), ("utility_iva_rate", a.utility_iva_rate), ("service_iva_rate", a.service_iva_rate) })
                        if (value is < 0 or > 100) p.Add($"aiu.{name}: debe estar entre 0 y 100.");
                }
                if (r.items is { Count: > 0 }) p.Add("items: en una factura AIU no se envían líneas; se generan a partir del bloque aiu.");
            }
            else if (r.items == null || r.items.Count == 0) p.Add("items: debe traer al menos una línea.");
            else ValidateItemLines(r.items, "items", p);

            return p;
        }

        // Forma de cada línea: la misma regla para los items de una factura y para las líneas de servicio de una AIU (path dice dónde están).
        internal static void ValidateItemLines(List<DataicoInvoiceItem> items, string path, List<string> p)
        {
            for (var i = 0; i < items.Count; i++)
            {
                var it = items[i];
                var at = $"{path}[{i}]";
                if (string.IsNullOrWhiteSpace(it.description)) p.Add($"{at}.description: es obligatorio.");
                if (it.quantity <= 0) p.Add($"{at}.quantity: debe ser mayor que 0.");
                if (it.price < 0) p.Add($"{at}.price: no puede ser negativo.");
                if (it.discount_rate < 0 || it.discount_rate > 100) p.Add($"{at}.discount_rate: debe estar entre 0 y 100.");
                foreach (var tax in it.taxes ?? new List<DataicoTax>())
                {
                    if (tax.tax_rate == null && tax.tax_amount != null)
                        p.Add($"{at}.taxes: el impuesto {tax.tax_category} llegó con valor fijo (tax_amount); por ahora solo se aceptan impuestos con porcentaje (tax_rate).");
                    else if (tax.tax_rate is < 0 or > 100) p.Add($"{at}.taxes: la tarifa de {tax.tax_category} debe estar entre 0 y 100.");
                }
                foreach (var ret in it.retentions ?? new List<DataicoTax>())
                    if (ret.tax_rate is null or < 0 or > 100) p.Add($"{at}.retentions: la retención {ret.tax_category} necesita tax_rate entre 0 y 100.");
                if (AiuContractData.IsAiuLine(it.sku)) p.Add($"{at}.sku: \"{it.sku}\" está reservado para las líneas de Administración, Imprevistos y Utilidad.");
            }
        }

        // ---------------------------------------------------------------------------------------------------------------------------
        // Resolución
        // ---------------------------------------------------------------------------------------------------------------------------
        internal async Task<(Resolution? Resolution, ApiError? Error)> FindResolutionAsync(Guid clientId, Guid branchId, DataicoNumbering numbering, string prefix, long number, string resolutionType)
        {
            var candidates = await _db.Resolutions.ForBranch(_db, branchId)
                .Where(r => r.ClientId == clientId && r.IsActive && r.DocumentType == resolutionType)
                .ToListAsync();

            var byPrefix = candidates.Where(r => string.Equals(r.Prefix ?? string.Empty, prefix, StringComparison.OrdinalIgnoreCase)).ToList();
            if (!string.IsNullOrWhiteSpace(numbering.resolution_number))
                byPrefix = byPrefix.Where(r => r.ResolutionNumber == numbering.resolution_number.Trim()).ToList();

            // Si el prefijo tiene resoluciones renovadas, se toma la que cubre el número pedido.
            var resolution = byPrefix.FirstOrDefault(r => number >= r.NumberStart && (r.NumberEnd <= 0 || number <= r.NumberEnd))
                             ?? byPrefix.OrderByDescending(r => r.ValidTo).FirstOrDefault();

            if (resolution == null)
            {
                var available = candidates.Count == 0
                    ? "Este emisor no tiene resoluciones activas de este tipo de documento para esta sucursal."
                    : "Resoluciones activas: " + string.Join(", ", candidates.Select(r => $"prefijo \"{r.Prefix}\" resolución {r.ResolutionNumber} ({r.NumberStart}-{r.NumberEnd})")) + ".";
                return (null, new ApiError(422, "RESOLUTION_NOT_FOUND", $"No hay una resolución activa de este tipo de documento con el prefijo \"{prefix}\"" +
                    (string.IsNullOrWhiteSpace(numbering.resolution_number) ? "." : $" y número {numbering.resolution_number}.") + " " + available));
            }

            if (resolution.NumberEnd > 0 && (number < resolution.NumberStart || number > resolution.NumberEnd))
                return (null, new ApiError(422, "NUMBER_OUT_OF_RANGE",
                    $"El número {number} está fuera del rango autorizado de la resolución {resolution.ResolutionNumber} (prefijo \"{resolution.Prefix}\"): {resolution.NumberStart} a {resolution.NumberEnd}."));

            var today = ColombiaTime.Now.Date;
            if (today < resolution.ValidFrom.Date || today > resolution.ValidTo.Date)
                return (null, new ApiError(422, "RESOLUTION_EXPIRED",
                    $"La resolución {resolution.ResolutionNumber} está vigente del {resolution.ValidFrom:dd/MM/yyyy} al {resolution.ValidTo:dd/MM/yyyy}; hoy no está en ese período."));

            return (resolution, null);
        }

        // ---------------------------------------------------------------------------------------------------------------------------
        // Tercero
        // ---------------------------------------------------------------------------------------------------------------------------
        // El rol (Cliente en la factura, Proveedor en el documento soporte) se respeta: se reutiliza solo un tercero de ese rol y, si no hay, se crea uno
        // nuevo con él aunque la misma identificación exista con otro rol (el índice único incluye el rol).
        internal async Task<(Customer? Customer, ApiError? Error)> EnsureCustomerAsync(Guid clientId, DataicoParty party, List<string> warnings, PartyType role = PartyType.Cliente)
        {
            var idTypes = await _db.IdentificationTypes.AsNoTracking().Where(t => t.IsActive).ToListAsync();
            var idType = IdentificationTypeResolver.Find(idTypes, party.party_identification_type);
            if (idType == null)
                return (null, new ApiError(400, "VALIDATION_ERROR", "El tipo de identificación del adquirente no es válido.",
                    new[] { $"customer.party_identification_type: \"{party.party_identification_type}\" no existe. Valores aceptados: {string.Join(", ", idTypes.Select(t => t.DataicoCode ?? t.Code).Distinct())}." }));

            // El número puede venir con el dígito de verificación ("900123456-7"): se guarda aparte.
            var raw = party.party_identification.Trim();
            var dash = raw.IndexOf('-');
            var number = new string((dash >= 0 ? raw[..dash] : raw).Where(char.IsLetterOrDigit).ToArray());
            var isNit = idType.Code == "31";
            var verification = dash >= 0 ? new string(raw[(dash + 1)..].Where(char.IsDigit).ToArray()) : string.Empty;
            if (isNit && verification.Length == 0) verification = NitValidation.CalculateCheckDigit(number).ToString(CultureInfo.InvariantCulture);

            var customer = await _db.Customers
                .Where(c => c.ClientId == clientId && c.IdentificationNumber == number && c.PartyType == role)
                .FirstOrDefaultAsync();
            if (customer == null)
            {
                customer = new Customer { Id = Guid.NewGuid(), ClientId = clientId, PartyType = role, CountryCode = "CO", CreatedAt = DateTime.UtcNow };
                _db.Customers.Add(customer);
            }

            var isJuridica = idType.Code is "31" or "50";
            customer.IdentificationType = idType.Code;
            customer.IdentificationNumber = number;
            if (verification.Length > 0) customer.VerificationDigit = verification;

            if (isJuridica)
            {
                customer.Name = FirstNonEmpty(party.company_name, TrimJoin(party.first_name, party.family_name));
            }
            else
            {
                var first = TrimJoin(party.first_name);
                var family = TrimJoin(party.family_name);
                customer.FirstName = first.Length > 0 ? first : customer.FirstName;
                customer.FirstLastName = family.Length > 0 ? family : customer.FirstLastName;
                customer.Name = FirstNonEmpty(TrimJoin(party.first_name, party.family_name), party.company_name);
            }

            if (!string.IsNullOrWhiteSpace(party.email)) customer.Email = party.email.Trim();
            if (!string.IsNullOrWhiteSpace(party.phone)) customer.Phone = party.phone.Trim();

            // Responsabilidad tributaria: sin dato se asume no responsable de IVA, el caso del consumidor final y de la persona natural.
            var level = string.IsNullOrWhiteSpace(party.tax_level_code) ? null : party.tax_level_code.Trim().ToUpperInvariant();
            if (level != null || string.IsNullOrWhiteSpace(customer.DataicoTaxLevelCode))
            {
                var responsable = level != null && !level.StartsWith("NO_") && (level.Contains("RESPONSABLE") || level == "COMUN" || level == "GRAN_CONTRIBUYENTE");
                customer.DataicoTaxLevelCode = level ?? "NO_RESPONSABLE_DE_IVA";
                customer.TaxRegime = responsable ? "48" : "49";
                if (string.IsNullOrWhiteSpace(customer.FiscalResponsibilities)) customer.FiscalResponsibilities = "R-99-PN";
            }
            var regimen = string.IsNullOrWhiteSpace(party.regimen) ? null : party.regimen.Trim().ToUpperInvariant();
            if (regimen != null || string.IsNullOrWhiteSpace(customer.DataicoRegimen))
            {
                customer.DataicoRegimen = regimen ?? "ORDINARIO";
                if (regimen == "SIMPLE") customer.IncomeTaxRegime = "47";
            }

            await ApplyAddressAsync(customer, party, warnings);
            customer.UpdatedAt = DateTime.UtcNow;
            return (customer, null);
        }

        // Dirección flexible: se acepta sin dirección y sin código DANE. Un municipio se resuelve por los segmentos DANE o por su nombre; si hay dirección
        // pero no se logra ubicar el municipio, la DIAN rechazaría el grupo, así que se omite la dirección y se avisa.
        private async Task ApplyAddressAsync(Customer customer, DataicoParty party, List<string> warnings)
        {
            var address = string.IsNullOrWhiteSpace(party.address_line) ? null : party.address_line.Trim();
            var country = string.IsNullOrWhiteSpace(party.country_code) ? "CO" : party.country_code.Trim().ToUpperInvariant();
            var department = party.department?.Trim() ?? string.Empty;
            var city = party.city?.Trim() ?? string.Empty;

            if (address == null && department.Length == 0 && city.Length == 0) return;

            customer.CountryCode = country;
            if (country != "CO")
            {
                customer.Address = address ?? customer.Address;
                customer.StateName = department.Length > 0 ? department : customer.StateName;
                customer.CityName = city.Length > 0 ? city : customer.CityName;
                customer.CityCode = string.Empty;
                if (CustomerAddressRules.IncompleteCityMessage(true, customer.Address, customer.CityCode, customer.CityName, customer.StateName) != null)
                {
                    warnings.Add("La dirección del adquirente se omitió: para un adquirente del exterior hay que enviar ciudad y departamento o estado.");
                    customer.Address = string.Empty; customer.CityName = string.Empty; customer.StateName = null;
                }
                return;
            }

            var municipalities = await _db.DianMunicipalities.AsNoTracking().ToListAsync();
            DianMunicipality? match = null;
            var dane = department + city;
            if (CustomerAddressRules.IsValidDaneCode(dane)) match = municipalities.FirstOrDefault(m => m.Code == dane);
            else if (CustomerAddressRules.IsValidDaneCode(city)) match = municipalities.FirstOrDefault(m => m.Code == city);
            if (match == null && city.Length > 0)
            {
                var wanted = Normalize(city);
                var byName = municipalities.Where(m => Normalize(m.Name) == wanted).ToList();
                if (byName.Count > 1 && department.Length > 0)
                {
                    var dept = Normalize(department);
                    var narrowed = byName.Where(m => m.DepartmentCode == department || Normalize(m.DepartmentName) == dept).ToList();
                    if (narrowed.Count > 0) byName = narrowed;
                }
                if (byName.Count == 1) match = byName[0];
            }

            if (match != null)
            {
                customer.CityCode = match.Code;
                customer.CityName = match.Name;
                customer.StateName = match.DepartmentName;
                if (address != null) customer.Address = address;
            }
            else if (address != null)
            {
                warnings.Add("La dirección del adquirente se omitió porque no se pudo ubicar el municipio. Envía customer.department y customer.city con el código DANE o el nombre del municipio.");
            }
        }

        // ---------------------------------------------------------------------------------------------------------------------------
        // Líneas
        // ---------------------------------------------------------------------------------------------------------------------------
        internal async Task<(List<DocumentItem> Items, ApiError? Error)> BuildItemsAsync(List<DataicoInvoiceItem> source, bool isMandate, List<string> warnings, string path = "items")
        {
            var units = await _db.UnitsOfMeasure.AsNoTracking().Where(u => u.IsActive).ToListAsync();
            var idTypes = await _db.IdentificationTypes.AsNoTracking().Where(t => t.IsActive).ToListAsync();
            var otherTaxes = (await _db.TaxCatalogItems.AsNoTracking().Where(t => t.IsActive && t.Kind == TaxCatalogKind.OtherTax).Select(t => t.Category).ToListAsync())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var retentionCategories = (await _db.TaxCatalogItems.AsNoTracking().Where(t => t.IsActive && t.Kind == TaxCatalogKind.Retention).Select(t => t.Category).ToListAsync())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var items = new List<DocumentItem>();
            var problems = new List<string>();
            for (var i = 0; i < source.Count; i++)
            {
                var s = source[i];
                var at = $"{path}[{i}]";

                var unitCode = string.IsNullOrWhiteSpace(s.measuring_unit) ? "94" : s.measuring_unit.Trim();
                var unit = units.FirstOrDefault(u => string.Equals(u.DianCode, unitCode, StringComparison.OrdinalIgnoreCase))
                           ?? units.FirstOrDefault(u => string.Equals(u.Abbreviation, unitCode, StringComparison.OrdinalIgnoreCase));
                if (unit == null)
                {
                    problems.Add($"{at}.measuring_unit: \"{unitCode}\" no es una unidad de medida válida; usa el código DIAN (por ejemplo 94 = unidad, KGM = kilogramo).");
                    continue;
                }

                var item = new DocumentItem
                {
                    Id = Guid.NewGuid(),
                    Code = string.IsNullOrWhiteSpace(s.sku) ? $"ITEM-{i + 1}" : s.sku.Trim(),
                    Name = s.description.Trim(),
                    Quantity = s.quantity,
                    UnitPrice = s.price,
                    DiscountRate = s.discount_rate,
                    UnitOfMeasureCode = unit.DianCode,
                    UnitOfMeasureAbbreviation = unit.Abbreviation,
                    UnitOfMeasureDisplayFormat = unit.DisplayFormat
                };

                // IVA: con tarifa mayor que 0 es gravado; con tarifa 0 es exento; sin impuesto de IVA, excluido (igual que lo envía el portal a Dataico).
                var iva = (s.taxes ?? new List<DataicoTax>()).FirstOrDefault(t => string.Equals(t.tax_category, "IVA", StringComparison.OrdinalIgnoreCase));
                if (iva == null) { item.IvaTreatment = IvaTreatment.Excluido; item.TaxRate = 0; }
                else if (iva.tax_rate is > 0) { item.IvaTreatment = IvaTreatment.Gravado; item.TaxRate = iva.tax_rate.Value; }
                else { item.IvaTreatment = IvaTreatment.Exento; item.TaxRate = 0; }

                foreach (var tax in (s.taxes ?? new List<DataicoTax>()).Where(t => !string.Equals(t.tax_category, "IVA", StringComparison.OrdinalIgnoreCase)))
                {
                    var category = tax.tax_category.Trim().ToUpperInvariant();
                    if (!otherTaxes.Contains(category))
                    {
                        problems.Add($"{at}.taxes: la categoría \"{tax.tax_category}\" no está disponible. Categorías aceptadas: IVA{(otherTaxes.Count > 0 ? ", " + string.Join(", ", otherTaxes.OrderBy(x => x)) : string.Empty)}.");
                        continue;
                    }
                    item.AdditionalTaxes.Add(new DocumentItemTax { Id = Guid.NewGuid(), DocumentItemId = item.Id, TaxCategory = category, Rate = tax.tax_rate ?? 0 });
                }

                foreach (var ret in s.retentions ?? new List<DataicoTax>())
                {
                    var category = ret.tax_category.Trim().ToUpperInvariant();
                    if (!retentionCategories.Contains(category))
                    {
                        problems.Add($"{at}.retentions: la categoría \"{ret.tax_category}\" no está disponible. Categorías aceptadas: {string.Join(", ", retentionCategories.OrderBy(x => x))}.");
                        continue;
                    }
                    item.Retentions.Add(new DocumentRetention { Id = Guid.NewGuid(), DocumentItemId = item.Id, TaxCategory = category, Rate = ret.tax_rate ?? 0 });
                }
                item.RetentionOverridden = item.Retentions.Count > 0;

                if (isMandate)
                {
                    if (string.IsNullOrWhiteSpace(s.mandante_identification) || string.IsNullOrWhiteSpace(s.mandante_identification_type))
                    {
                        problems.Add($"{at}: en una factura de mandato cada línea necesita mandante_identification y mandante_identification_type.");
                    }
                    else
                    {
                        var mandanteType = IdentificationTypeResolver.Find(idTypes, s.mandante_identification_type);
                        if (mandanteType == null) problems.Add($"{at}.mandante_identification_type: \"{s.mandante_identification_type}\" no es un tipo de identificación válido.");
                        else
                        {
                            item.MandanteIdentificationType = mandanteType.Code;
                            item.MandanteIdentification = s.mandante_identification.Trim();
                        }
                    }
                }

                items.Add(item);
            }

            return problems.Count > 0
                ? (items, new ApiError(400, "VALIDATION_ERROR", "El documento tiene datos inválidos o faltantes. Revisa el detalle de cada campo.", problems))
                : (items, null);
        }

        // ---------------------------------------------------------------------------------------------------------------------------
        // Moneda y descuentos o cargos generales
        // ---------------------------------------------------------------------------------------------------------------------------
        private async Task<ApiError?> ApplyCurrencyAsync(Document document, DataicoInvoiceRequest request, Client client)
        {
            var currency = (request.currency ?? string.Empty).Trim().ToUpperInvariant();
            if (currency.Length == 0 || currency == "COP")
            {
                document.Currency = "COP"; document.ExchangeRate = null; document.ExchangeRateDate = null;
                return null;
            }

            if (!client.ForeignCurrencyEnabled)
                return new ApiError(422, "FOREIGN_CURRENCY_DISABLED", "Este emisor no tiene habilitada la facturación en moneda extranjera. Actívala en el portal.");
            if (!await _db.TaxCatalogItems.AnyAsync(t => t.Kind == TaxCatalogKind.Currency && t.Category == currency && t.IsActive))
                return new ApiError(400, "VALIDATION_ERROR", "La moneda no está disponible.", new[] { $"currency: \"{currency}\" no es una moneda válida; usa su código ISO 4217 (por ejemplo USD)." });
            if (request.currency_exchange_rate is not > 0 || ParseDate(request.currency_exchange_rate_date) == null)
                return new ApiError(400, "VALIDATION_ERROR", "Falta la tasa de cambio.", new[] { "currency_exchange_rate y currency_exchange_rate_date (dd/MM/yyyy) son obligatorios con una moneda distinta de COP." });

            document.Currency = currency;
            document.ExchangeRate = request.currency_exchange_rate;
            document.ExchangeRateDate = ParseDate(request.currency_exchange_rate_date);
            return null;
        }

        internal static void ApplyCharges(Document document, List<DataicoCharge>? charges)
        {
            document.GeneralDiscountAmount = document.GeneralChargeAmount = null;
            document.GeneralDiscountReason = document.GeneralChargeReason = null;
            foreach (var charge in charges ?? new List<DataicoCharge>())
            {
                if (charge.base_amount <= 0) continue;
                if (charge.discount) { document.GeneralDiscountAmount = (document.GeneralDiscountAmount ?? 0) + charge.base_amount; document.GeneralDiscountReason = charge.reason; }
                else { document.GeneralChargeAmount = (document.GeneralChargeAmount ?? 0) + charge.base_amount; document.GeneralChargeReason = charge.reason; }
            }
        }

        // ---------------------------------------------------------------------------------------------------------------------------
        // Utilidades
        // ---------------------------------------------------------------------------------------------------------------------------
        internal static DateTime? ParseDate(string? value) =>
            DateTime.TryParseExact(value?.Trim(), new[] { "dd/MM/yyyy", "yyyy-MM-dd" }, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ? date : null;

        private static string TrimJoin(params string?[] parts) => string.Join(" ", parts.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p!.Trim()));
        private static string FirstNonEmpty(params string?[] values) => values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))?.Trim() ?? string.Empty;

        // El medio de pago se guarda con la clave del catálogo (CASH, DEBIT_CARD...), que es la que traduce al código DIAN. Se acepta esa clave,
        // el código DIAN (10, 48...) o el nombre en español ("Efectivo"). A crédito no aplica.
        internal async Task<(string? Category, ApiError? Error)> ResolvePaymentMeansAsync(DataicoInvoiceRequest request)
        {
            if (string.Equals(request.payment_means_type?.Trim(), "CREDITO", StringComparison.OrdinalIgnoreCase)) return (null, null);

            var catalog = await _db.TaxCatalogItems.AsNoTracking().Where(i => i.Kind == TaxCatalogKind.PaymentMeans && i.IsActive).ToListAsync();
            var wanted = Normalize(request.payment_means ?? string.Empty).Replace(' ', '_');
            var match = catalog.FirstOrDefault(i => Normalize(i.Category).Replace(' ', '_') == wanted)
                ?? catalog.FirstOrDefault(i => i.DianCode == request.payment_means!.Trim())
                ?? catalog.FirstOrDefault(i => Normalize(i.Name).Replace(' ', '_') == wanted);
            if (match != null) return (match.Category, null);

            var common = catalog.Where(i => i.DianCode is "10" or "30" or "31" or "42" or "48" or "49" or "ZZZ").Select(i => $"{i.Category} ({i.Name})").Distinct().ToList();
            var detail = $"payment_means: \"{request.payment_means}\" no es un medio de pago válido. Usa la clave del catálogo, por ejemplo: {string.Join(", ", common.Count > 0 ? common : catalog.Take(8).Select(i => i.Category))}.";
            return (null, new ApiError(400, "VALIDATION_ERROR", "El documento tiene datos inválidos o faltantes. Revisa el detalle de cada campo.", new List<string> { detail }));
        }

        // Compara nombres sin tildes ni mayúsculas ("BOGOTÁ, D.C." ~ "bogota d.c.").
        private static string Normalize(string value)
        {
            var decomposed = value.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder();
            foreach (var ch in decomposed)
                if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark) sb.Append(ch);
            return sb.ToString();
        }
    }

    internal static class InvoiceBuildResultExtensions
    {
        public static InvoiceBuildResult WithWarnings(this InvoiceBuildResult result, IEnumerable<string> warnings)
        {
            result.Warnings.AddRange(warnings);
            return result;
        }
    }
}
