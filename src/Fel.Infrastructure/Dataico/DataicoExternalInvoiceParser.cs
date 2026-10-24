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

    private static decimal Decimal(string value) =>
        decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var result) ? result : 0m;

    public static DataicoExternalInvoiceDraft Parse(string xml, string dataicoDocumentId)
    {
        var root = XDocument.Parse(xml, LoadOptions.PreserveWhitespace).Root
            ?? throw new InvalidOperationException("El XML de Dataico no tiene un elemento raíz.");
        if (root.Name.LocalName != "Invoice")
            throw new InvalidOperationException($"El XML consultado no es una factura: '{root.Name.LocalName}'.");

        var customerParty = root.Descendants().FirstOrDefault(e => e.Name.LocalName == "AccountingCustomerParty")?.Elements().FirstOrDefault(e => e.Name.LocalName == "Party");
        var taxScheme = customerParty?.Elements().FirstOrDefault(e => e.Name.LocalName == "PartyTaxScheme");
        var customerTaxId = Value(taxScheme, "CompanyID");
        var customerName = Value(taxScheme, "RegistrationName");
        if (string.IsNullOrWhiteSpace(customerName)) customerName = Value(customerParty?.Elements().FirstOrDefault(e => e.Name.LocalName == "PartyLegalEntity"), "RegistrationName");
        if (string.IsNullOrWhiteSpace(customerName)) customerName = Value(customerParty?.Elements().FirstOrDefault(e => e.Name.LocalName == "PartyName"), "Name");

        var payment = root.Descendants().FirstOrDefault(e => e.Name.LocalName == "PaymentMeans");
        var paymentCode = Value(payment, "ID");
        var dueDate = Value(payment, "PaymentDueDate");
        int? termDays = null;
        if (DateTime.TryParse(dueDate, out var due) && DateTime.TryParse(Value(root, "IssueDate"), out var issueForTerm))
            termDays = Math.Max(0, (due.Date - issueForTerm.Date).Days);

        var lines = root.Descendants().Where(e => e.Name.LocalName == "InvoiceLine").Select(line =>
        {
            var quantityNode = line.Elements().FirstOrDefault(e => e.Name.LocalName == "InvoicedQuantity");
            var priceNode = line.Elements().FirstOrDefault(e => e.Name.LocalName == "Price");
            var item = line.Elements().FirstOrDefault(e => e.Name.LocalName == "Item");
            var taxSubtotal = line.Descendants().FirstOrDefault(e => e.Name.LocalName == "TaxSubtotal");
            var taxCategory = taxSubtotal?.Descendants().FirstOrDefault(e => e.Name.LocalName == "TaxCategory");
            var taxSchemeNode = taxCategory?.Elements().FirstOrDefault(e => e.Name.LocalName == "TaxScheme");
            var taxId = Value(taxSchemeNode, "ID").ToUpperInvariant();
            var percent = Decimal(Value(taxCategory, "Percent"));
            var treatment = taxId is "04" or "E" ? "Exento" : taxId is "ZZ" or "O" ? "Excluido" : "Gravado";
            return new DataicoExternalInvoiceLine
            {
                Code = item?.Descendants().FirstOrDefault(e => e.Name.LocalName is "ID" or "StandardItemIdentification")?.Value.Trim() ?? string.Empty,
                Name = Value(item, "Description"),
                Quantity = Decimal(quantityNode?.Value ?? "1"),
                UnitPrice = Decimal(Value(priceNode, "PriceAmount")),
                UnitOfMeasureCode = quantityNode?.Attribute("unitCode")?.Value ?? "94",
                IvaTreatment = treatment,
                TaxRate = treatment == "Gravado" ? percent : 0,
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
            Subtotal = Decimal(Value(legalTotal, "LineExtensionAmount")),
            TaxAmount = Decimal(Value(taxTotal, "TaxAmount")),
            TotalAmount = Decimal(Value(legalTotal, "PayableAmount")),
            Items = lines
        };
    }
}
