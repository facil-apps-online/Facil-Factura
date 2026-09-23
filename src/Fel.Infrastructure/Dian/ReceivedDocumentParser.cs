using System;
using System.Linq;
using System.Xml.Linq;

namespace Fel.Infrastructure.Dian
{
    public class ParsedReceivedDocument
    {
        public string DocumentTypeCode { get; set; } = string.Empty; // 01=Factura, 91=NC, 92=ND
        public string Cufe { get; set; } = string.Empty;
        public string DocumentId { get; set; } = string.Empty;
        public string IssuerTaxId { get; set; } = string.Empty;
        public string IssuerName { get; set; } = string.Empty;
        public DateTime IssueDate { get; set; }
        public decimal TotalAmount { get; set; }
    }

    // Parsea el XML UBL de una factura/nota de un TERCERO (no emitida por nosotros) para extraer
    // los datos necesarios para generar el Evento de Recepción. Busca por nombre local de elemento
    // (no por namespace exacto) para tolerar variaciones menores entre distintos proveedores
    // tecnológicos — solo el root (Invoice/CreditNote/DebitNote) es lo que define el tipo.
    public static class ReceivedDocumentParser
    {
        public static ParsedReceivedDocument Parse(string xml)
        {
            var doc = XDocument.Parse(xml);
            var root = doc.Root ?? throw new InvalidOperationException("El XML no tiene un elemento raíz.");

            var documentTypeCode = root.Name.LocalName switch
            {
                "Invoice" => "01",
                "CreditNote" => "91",
                "DebitNote" => "92",
                _ => throw new NotSupportedException($"Tipo de documento no soportado para recepción: '{root.Name.LocalName}'. Se esperaba Invoice, CreditNote o DebitNote.")
            };

            string El(XElement parent, params string[] path)
            {
                XElement? current = parent;
                foreach (var name in path)
                {
                    current = current?.Elements().FirstOrDefault(e => e.Name.LocalName == name);
                    if (current == null) return string.Empty;
                }
                return current.Value.Trim();
            }

            XElement? Find(XElement parent, params string[] path)
            {
                XElement? current = parent;
                foreach (var name in path)
                {
                    current = current?.Elements().FirstOrDefault(e => e.Name.LocalName == name);
                    if (current == null) return null;
                }
                return current;
            }

            var cufe = El(root, "UUID");
            var documentId = El(root, "ID");
            var issueDateStr = El(root, "IssueDate");

            var supplierParty = Find(root, "AccountingSupplierParty", "Party");
            var issuerTaxId = supplierParty != null ? El(supplierParty, "PartyTaxScheme", "CompanyID") : string.Empty;
            var issuerName = supplierParty != null
                ? (El(supplierParty, "PartyTaxScheme", "RegistrationName") is { Length: > 0 } regName ? regName : El(supplierParty, "PartyName", "Name"))
                : string.Empty;

            var payableAmountStr = El(root, "LegalMonetaryTotal", "PayableAmount");

            if (string.IsNullOrWhiteSpace(cufe))
                throw new InvalidOperationException("El XML no trae un CUFE/CUDE (cbc:UUID) — no parece ser un documento electrónico válido de la DIAN.");

            return new ParsedReceivedDocument
            {
                DocumentTypeCode = documentTypeCode,
                Cufe = cufe,
                DocumentId = documentId,
                IssuerTaxId = issuerTaxId,
                IssuerName = issuerName,
                IssueDate = DateTime.TryParse(issueDateStr, out var issueDate) ? issueDate : DateTime.UtcNow,
                TotalAmount = decimal.TryParse(payableAmountStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var total) ? total : 0m
            };
        }
    }
}
