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

    // Datos del proveedor (emisor del documento recibido) con los valores que Dataico espera en el bloque
    // "receiver" de un evento de recepción. Lo que el XML no trae queda con el mismo valor por defecto que usa
    // el legacy FEL (que ya envía estos eventos en producción).
    public class ParsedIssuer
    {
        public string IdentificationType { get; set; } = "NIT";
        public string Identification { get; set; } = string.Empty;
        public string PartyType { get; set; } = "PERSONA_JURIDICA";
        public string TaxLevelCode { get; set; } = "RESPONSABLE_DE_IVA";
        public string Regimen { get; set; } = "ORDINARIO";
        public string CompanyName { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string FamilyName { get; set; } = string.Empty;
        public string Address { get; set; } = "DIRECCION";
        public string City { get; set; } = "001";
        public string Department { get; set; } = "11";
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = "0000000";
    }

    // Parsea el XML UBL de una factura/nota de un TERCERO (no emitida por nosotros) para extraer
    // los datos necesarios para generar el Evento de Recepción. Busca por nombre local de elemento
    // (no por namespace exacto) para tolerar variaciones menores entre distintos proveedores
    // tecnológicos — solo el root (Invoice/CreditNote/DebitNote) es lo que define el tipo.
    public static class ReceivedDocumentParser
    {
        private static string Norm(string? value) => string.Join(' ', (value ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

        private static string IdentificationTypeFor(string? code) => code switch
        {
            "13" => "CC",
            "22" => "CE",
            "41" => "PASAPORTE",
            _ => "NIT" // 31 y cualquier otro
        };

        // Responsabilidades de la DIAN (cbc:TaxLevelCode, a veces varias separadas por ';') al par que pide Dataico.
        private static (string TaxLevel, string Regimen) TaxLevelFor(string? codes)
        {
            var list = (codes ?? string.Empty).ToUpperInvariant().Split(new[] { ';', ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (list.Contains("O-47")) return ("SIMPLIFICADO", "SIMPLE");
            if (list.Contains("R-99-PN")) return ("NO_RESPONSABLE_DE_IVA", "ORDINARIO");
            return ("RESPONSABLE_DE_IVA", "ORDINARIO");
        }

        // Teléfono solo con dígitos, de máximo 10: así lo exige Dataico (mismo saneamiento del legacy).
        private static string CleanPhone(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "0000000";
            var first = raw.Split(new[] { '-', '/', '|' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty;
            var digits = new string(first.Where(char.IsDigit).ToArray());
            if (digits.StartsWith("03")) digits = "60" + digits[2..];
            if (digits.Length > 10) digits = digits[..10];
            return digits.Length == 0 ? "0000000" : digits;
        }

        // Nombres y apellidos de una persona natural. Algunos emisores mandan los apellidos en FirstName/MiddleName
        // y sin FamilyName; en ese caso la razón social ("APELLIDOS NOMBRES") es la fuente de verdad.
        private static (string First, string Family) SplitPerson(XElement person, string registrationName)
        {
            string Child(string name) => Norm(person.Elements().FirstOrDefault(e => e.Name.LocalName == name)?.Value);
            var first = Child("FirstName");
            var middle = Child("MiddleName");
            var family = Child("FamilyName");

            if (family.Length > 0) return (Norm(first + " " + middle), family);

            var tokens = Norm(first + " " + middle);
            var registration = Norm(registrationName);
            if (tokens.Length > 0 && registration.StartsWith(tokens, StringComparison.OrdinalIgnoreCase) && registration.Length > tokens.Length)
                return (registration[tokens.Length..].Trim(), tokens);
            return (tokens, string.Empty);
        }

        // Datos del emisor (AccountingSupplierParty) para el bloque "receiver" del evento de Dataico.
        public static ParsedIssuer ParseIssuer(string xml)
        {
            var root = XDocument.Parse(xml).Root ?? throw new InvalidOperationException("El XML no tiene un elemento raíz.");
            var issuer = new ParsedIssuer();

            XElement? Find(XElement? parent, params string[] path)
            {
                var current = parent;
                foreach (var name in path)
                {
                    current = current?.Elements().FirstOrDefault(e => e.Name.LocalName == name);
                    if (current == null) return null;
                }
                return current;
            }

            var party = Find(root, "AccountingSupplierParty", "Party");
            if (party == null) return issuer;

            var taxScheme = Find(party, "PartyTaxScheme");
            var companyId = Find(taxScheme, "CompanyID");
            issuer.Identification = companyId?.Value.Trim() ?? string.Empty;
            issuer.IdentificationType = IdentificationTypeFor(companyId?.Attribute("schemeName")?.Value);

            var registrationName = Find(taxScheme, "RegistrationName")?.Value;
            if (string.IsNullOrWhiteSpace(registrationName)) registrationName = Find(party, "PartyLegalEntity", "RegistrationName")?.Value;
            if (string.IsNullOrWhiteSpace(registrationName)) registrationName = Find(party, "PartyName", "Name")?.Value;
            issuer.CompanyName = Norm(registrationName);

            var person = Find(party, "Person");
            if (person != null)
            {
                issuer.PartyType = "PERSONA_NATURAL";
                (issuer.FirstName, issuer.FamilyName) = SplitPerson(person, issuer.CompanyName);
            }

            (issuer.TaxLevelCode, issuer.Regimen) = TaxLevelFor(Find(taxScheme, "TaxLevelCode")?.Value);

            var address = Find(party, "PhysicalLocation", "Address");
            var department = Find(address, "CountrySubentityCode")?.Value.Trim();
            var city = Find(address, "ID")?.Value.Trim();
            var line = Find(address, "AddressLine", "Line")?.Value.Trim();
            if (!string.IsNullOrEmpty(department)) issuer.Department = department;
            if (!string.IsNullOrEmpty(city)) issuer.City = city;
            if (!string.IsNullOrEmpty(line)) issuer.Address = line;

            var contact = Find(party, "Contact");
            issuer.Email = Find(contact, "ElectronicMail")?.Value.Trim() ?? string.Empty;
            issuer.Phone = CleanPhone(Find(contact, "Telephone")?.Value);
            return issuer;
        }

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
                IssueDate = DateTime.TryParse(issueDateStr, out var issueDate) ? issueDate : Fel.Core.Models.ColombiaTime.Now,
                TotalAmount = decimal.TryParse(payableAmountStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var total) ? total : 0m
            };
        }
    }
}
