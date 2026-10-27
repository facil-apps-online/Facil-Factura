using System.Globalization;
using System.Xml.Linq;

namespace Fel.Infrastructure.Dataico;

public sealed class DataicoExternalInvoiceDraft
{
    public string DataicoDocumentId { get; init; } = string.Empty;
    public string Number { get; init; } = string.Empty;
    public string Cufe { get; init; } = string.Empty;
    public DateTime IssueDate { get; init; }
    public string Currency { get; init; } = "COP";
    public string? PaymentMeans { get; init; }
    public string PaymentMeansType { get; init; } = "CONTADO";
    public int? PaymentTermDays { get; init; }
    public string CustomerTaxId { get; init; } = string.Empty;
    public string CustomerName { get; init; } = string.Empty;
    // Lo que la factura trae del adquirente, para crearlo en el sistema si todavía no existe.
    public string CustomerIdentificationType { get; init; } = string.Empty;
    public string CustomerVerificationDigit { get; init; } = string.Empty;
    public string CustomerEmail { get; init; } = string.Empty;
    public string CustomerPhone { get; init; } = string.Empty;
    public string CustomerAddress { get; init; } = string.Empty;
    public string CustomerCityCode { get; init; } = string.Empty;
    public string CustomerCityName { get; init; } = string.Empty;
    public string CustomerStateName { get; init; } = string.Empty;
    public string CustomerCountryCode { get; init; } = "CO";
    // "01" = responsable de IVA, "ZZ" = no aplica.
    public string CustomerTaxSchemeId { get; init; } = string.Empty;
    public IReadOnlyList<string> CustomerTaxLevelCodes { get; init; } = Array.Empty<string>();
    public decimal Subtotal { get; init; }
    public decimal TaxAmount { get; init; }
    public decimal TotalAmount { get; init; }
    public IReadOnlyList<DataicoExternalInvoiceLine> Items { get; init; } = Array.Empty<DataicoExternalInvoiceLine>();
}

public sealed class DataicoExternalInvoiceLine
{
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public decimal Quantity { get; init; }
    public decimal UnitPrice { get; init; }
    public string UnitOfMeasureCode { get; init; } = "94";
    public string UnitOfMeasureAbbreviation { get; init; } = "EA";
    public string IvaTreatment { get; init; } = "Gravado";
    public decimal TaxRate { get; init; }
    public decimal DiscountRate { get; init; }
    public decimal TaxAmount { get; init; }
    public decimal TotalAmount { get; init; }
}

public static class DataicoExternalInvoiceParser
{
    private static string Value(XElement? parent, string name) =>
        parent?.Elements().FirstOrDefault(e => e.Name.LocalName == name)?.Value.Trim() ?? string.Empty;

    private static XElement? Child(XElement? parent, string name) =>
        parent?.Elements().FirstOrDefault(e => e.Name.LocalName == name);

    private static decimal Decimal(string value) =>
        decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var result) ? result : 0m;

    // Las facturas ya aceptadas por la DIAN llegan de Dataico dentro de un AttachedDocument: la factura va como texto (CDATA) en la descripción del
    // adjunto, junto a la respuesta de la DIAN. Se busca la que tenga como raíz una Invoice.
    private static XElement? UnwrapInvoice(XElement attached)
    {
        foreach (var description in attached.Descendants().Where(e => e.Name.LocalName == "Description"))
        {
            var text = description.Value.Trim();
            if (!text.StartsWith('<')) continue;
            try
            {
                var inner = XDocument.Parse(text, LoadOptions.PreserveWhitespace).Root;
                if (inner?.Name.LocalName == "Invoice") return inner;
            }
            catch (System.Xml.XmlException)
            {
                // No era un XML (o no era el de la factura): se sigue con el siguiente adjunto.
            }
        }
        return null;
    }

    public static DataicoExternalInvoiceDraft Parse(string xml, string dataicoDocumentId)
    {
        var root = XDocument.Parse(xml, LoadOptions.PreserveWhitespace).Root
            ?? throw new InvalidOperationException("El XML de Dataico no tiene un elemento raíz.");
        if (root.Name.LocalName == "AttachedDocument")
            root = UnwrapInvoice(root) ?? throw new InvalidOperationException("El documento adjunto de Dataico no trae la factura dentro.");
        if (root.Name.LocalName != "Invoice")
            throw new InvalidOperationException($"El XML consultado no es una factura: '{root.Name.LocalName}'.");

        var customerParty = Child(root.Descendants().FirstOrDefault(e => e.Name.LocalName == "AccountingCustomerParty"), "Party");
        var taxScheme = Child(customerParty, "PartyTaxScheme");
        var companyId = Child(taxScheme, "CompanyID");
        var customerTaxId = companyId?.Value.Trim() ?? string.Empty;
        var customerName = Value(taxScheme, "RegistrationName");
        if (string.IsNullOrWhiteSpace(customerName)) customerName = Value(Child(customerParty, "PartyLegalEntity"), "RegistrationName");
        if (string.IsNullOrWhiteSpace(customerName)) customerName = Value(Child(customerParty, "PartyName"), "Name");

        // El tipo de documento sale del esquema del identificador (13 cédula, 31 NIT...); el dígito de verificación solo aplica al NIT.
        var partyId = Child(Child(customerParty, "PartyIdentification"), "ID");
        var idType = (companyId ?? partyId)?.Attribute("schemeName")?.Value.Trim() ?? string.Empty;
        var verificationDigit = idType == "31" ? (companyId ?? partyId)?.Attribute("schemeID")?.Value.Trim() ?? string.Empty : string.Empty;
        if (string.IsNullOrWhiteSpace(customerTaxId)) customerTaxId = partyId?.Value.Trim() ?? string.Empty;

        var address = Child(Child(customerParty, "PhysicalLocation"), "Address") ?? Child(taxScheme, "RegistrationAddress");
        var contact = Child(customerParty, "Contact");
        var taxLevelCodes = Value(taxScheme, "TaxLevelCode")
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var payment = root.Descendants().FirstOrDefault(e => e.Name.LocalName == "PaymentMeans");
        var paymentCode = Value(payment, "ID");
        var dueDate = Value(payment, "PaymentDueDate");
        int? termDays = null;
        if (DateTime.TryParse(dueDate, out var due) && DateTime.TryParse(Value(root, "IssueDate"), out var issueForTerm))
            termDays = Math.Max(0, (due.Date - issueForTerm.Date).Days);

        var lines = root.Descendants().Where(e => e.Name.LocalName == "InvoiceLine").Select(line =>
        {
            var quantityNode = Child(line, "InvoicedQuantity");
            var priceNode = Child(line, "Price");
            var item = Child(line, "Item");
            var taxSubtotal = line.Descendants().FirstOrDefault(e => e.Name.LocalName == "TaxSubtotal");
            var taxCategory = taxSubtotal?.Descendants().FirstOrDefault(e => e.Name.LocalName == "TaxCategory");
            var taxSchemeNode = Child(taxCategory, "TaxScheme");
            var taxId = Value(taxSchemeNode, "ID").ToUpperInvariant();
            var percent = Decimal(Value(taxCategory, "Percent"));
            var treatment = taxId is "04" or "E" ? "Exento" : taxId is "ZZ" or "O" ? "Excluido" : "Gravado";

            // Código del producto: el del vendedor; si no hay, el estándar.
            var sellerCode = Value(Child(item, "SellersItemIdentification"), "ID");
            var standardCode = Value(Child(item, "StandardItemIdentification"), "ID");

            // Descuento de la línea (cargo con indicador falso): porcentaje sobre su base.
            var discount = line.Elements().FirstOrDefault(e => e.Name.LocalName == "AllowanceCharge" && Value(e, "ChargeIndicator").Equals("false", StringComparison.OrdinalIgnoreCase));
            var discountAmount = Decimal(Value(discount, "Amount"));
            var discountBase = Decimal(Value(discount, "BaseAmount"));
            var discountRate = discount != null && discountBase > 0 ? Math.Round(discountAmount / discountBase * 100, 4) : 0m;

            return new DataicoExternalInvoiceLine
            {
                Code = !string.IsNullOrWhiteSpace(sellerCode) ? sellerCode : standardCode,
                Name = Value(item, "Description"),
                Quantity = Decimal(quantityNode?.Value ?? "1"),
                UnitPrice = Decimal(Value(priceNode, "PriceAmount")),
                UnitOfMeasureCode = quantityNode?.Attribute("unitCode")?.Value ?? "94",
                IvaTreatment = treatment,
                TaxRate = treatment == "Gravado" ? percent : 0,
                DiscountRate = discountRate,
                TaxAmount = Decimal(Value(taxSubtotal, "TaxAmount")),
                TotalAmount = Decimal(Value(line, "LineExtensionAmount"))
            };
        }).ToList();

        var taxTotal = root.Elements().FirstOrDefault(e => e.Name.LocalName == "TaxTotal");
        var legalTotal = root.Elements().FirstOrDefault(e => e.Name.LocalName == "LegalMonetaryTotal");
        var issue = DateTime.TryParse(Value(root, "IssueDate"), out var issueDate) ? issueDate : Fel.Core.Models.ColombiaTime.Now;
        return new DataicoExternalInvoiceDraft
        {
            DataicoDocumentId = dataicoDocumentId,
            Number = Value(root, "ID"),
            Cufe = Value(root, "UUID"),
            IssueDate = issue,
            Currency = root.Elements().FirstOrDefault(e => e.Name.LocalName == "DocumentCurrencyCode")?.Value.Trim() ?? "COP",
            PaymentMeans = paymentCode,
            PaymentMeansType = paymentCode == "2" ? "CREDITO" : "CONTADO",
            PaymentTermDays = termDays,
            CustomerTaxId = customerTaxId,
            CustomerName = customerName,
            CustomerIdentificationType = idType,
            CustomerVerificationDigit = verificationDigit,
            CustomerEmail = Value(contact, "ElectronicMail"),
            CustomerPhone = Value(contact, "Telephone"),
            CustomerAddress = Value(Child(address, "AddressLine"), "Line"),
            CustomerCityCode = Value(address, "ID"),
            CustomerCityName = Value(address, "CityName"),
            CustomerStateName = Value(address, "CountrySubentity"),
            CustomerCountryCode = Value(Child(address, "Country"), "IdentificationCode") is { Length: > 0 } country ? country.ToUpperInvariant() : "CO",
            CustomerTaxSchemeId = Value(Child(taxScheme, "TaxScheme"), "ID").ToUpperInvariant(),
            CustomerTaxLevelCodes = taxLevelCodes,
            Subtotal = Decimal(Value(legalTotal, "LineExtensionAmount")),
            TaxAmount = Decimal(Value(taxTotal, "TaxAmount")),
            TotalAmount = Decimal(Value(legalTotal, "PayableAmount")),
            Items = lines
        };
    }
}
