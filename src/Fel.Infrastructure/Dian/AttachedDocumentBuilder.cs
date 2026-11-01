using System.Xml.Linq;

namespace Fel.Infrastructure.Dian
{
    // Arma el contenedor AttachedDocument (Anexo Técnico, numeral 6.4): un único XML con la factura/nota firmada y la respuesta de aprobación de
    // la DIAN. Es lo que el emisor entrega al adquirente en el correo (numeral 9.1). Todo lo que necesita sale de los dos XML ya generados, así
    // que no depende de los datos actuales del documento. Devuelve el XML SIN firmar, con las dos ext:UBLExtension que exige el firmador.
    public static class AttachedDocumentBuilder
    {
        private static readonly XNamespace Doc = "urn:oasis:names:specification:ubl:schema:xsd:AttachedDocument-2";
        private static readonly XNamespace Cac = "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2";
        private static readonly XNamespace Cbc = "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2";
        private static readonly XNamespace Ccts = "urn:un:unece:uncefact:data:specification:CoreComponentTypeSchemaModule:2";
        private static readonly XNamespace Ds = "http://www.w3.org/2000/09/xmldsig#";
        private static readonly XNamespace Ext = "urn:oasis:names:specification:ubl:schema:xsd:CommonExtensionComponents-2";
        private static readonly XNamespace Xades = "http://uri.etsi.org/01903/v1.3.2#";
        private static readonly XNamespace Xades141 = "http://uri.etsi.org/01903/v1.4.1#";

        public static string Build(string signedDocumentXml, string applicationResponseXml, int containerId, DateTime nowColombia)
        {
            var document = XDocument.Parse(signedDocumentXml);
            var response = XDocument.Parse(applicationResponseXml);

            var root = document.Root ?? throw new InvalidOperationException("El documento firmado no tiene contenido.");
            string Text(XElement? e, string what) => e?.Value.Trim() is { Length: > 0 } v ? v : throw new InvalidOperationException($"El documento firmado no trae {what}.");

            var parentId = Text(root.Element(Cbc + "ID"), "el número (cbc:ID)");
            var cufe = Text(root.Element(Cbc + "UUID"), "el CUFE (cbc:UUID)");
            var issueDate = Text(root.Element(Cbc + "IssueDate"), "la fecha de emisión");
            var profileExecution = root.Element(Cbc + "ProfileExecutionID")?.Value.Trim() is { Length: > 0 } p ? p : "1";

            var sender = PartyTaxScheme(root, "AccountingSupplierParty") ?? throw new InvalidOperationException("El documento firmado no trae los datos del emisor.");
            var receiver = PartyTaxScheme(root, "AccountingCustomerParty") ?? throw new InvalidOperationException("El documento firmado no trae los datos del adquirente.");

            // Fecha y hora en que la DIAN validó el documento: salen de su propia respuesta.
            var validationDate = response.Root?.Element(Cbc + "IssueDate")?.Value.Trim() is { Length: > 0 } vd ? vd : nowColombia.ToString("yyyy-MM-dd");
            var validationTime = response.Root?.Element(Cbc + "IssueTime")?.Value.Trim() is { Length: > 0 } vt ? vt : nowColombia.ToString("HH:mm:ss") + "-05:00";

            var attached = new XElement(Doc + "AttachedDocument",
                new XAttribute(XNamespace.Xmlns + "cac", Cac.NamespaceName),
                new XAttribute(XNamespace.Xmlns + "cbc", Cbc.NamespaceName),
                new XAttribute(XNamespace.Xmlns + "ccts", Ccts.NamespaceName),
                new XAttribute(XNamespace.Xmlns + "ds", Ds.NamespaceName),
                new XAttribute(XNamespace.Xmlns + "ext", Ext.NamespaceName),
                new XAttribute(XNamespace.Xmlns + "xades", Xades.NamespaceName),
                new XAttribute(XNamespace.Xmlns + "xades141", Xades141.NamespaceName),
                // La primera extensión queda vacía y la segunda recibe la firma XAdES (ver XadesSigner).
                new XElement(Ext + "UBLExtensions",
                    new XElement(Ext + "UBLExtension", new XElement(Ext + "ExtensionContent")),
                    new XElement(Ext + "UBLExtension", new XElement(Ext + "ExtensionContent"))),
                new XElement(Cbc + "UBLVersionID", "UBL 2.1"),
                new XElement(Cbc + "CustomizationID", "Documentos adjuntos"),
                new XElement(Cbc + "ProfileID", "Factura Electrónica de Venta"),
                new XElement(Cbc + "ProfileExecutionID", profileExecution),
                new XElement(Cbc + "ID", containerId.ToString()),
                new XElement(Cbc + "IssueDate", nowColombia.ToString("yyyy-MM-dd")),
                new XElement(Cbc + "IssueTime", nowColombia.ToString("HH:mm:ss") + "-05:00"),
                new XElement(Cbc + "DocumentType", "Contenedor de Factura Electrónica"),
                new XElement(Cbc + "ParentDocumentID", parentId),
                new XElement(Cac + "SenderParty", sender),
                new XElement(Cac + "ReceiverParty", receiver),
                new XElement(Cac + "Attachment",
                    new XElement(Cac + "ExternalReference",
                        new XElement(Cbc + "MimeCode", "text/xml"),
                        new XElement(Cbc + "EncodingCode", "UTF-8"),
                        new XElement(Cbc + "Description", new XCData(WithoutDeclaration(signedDocumentXml))))),
                new XElement(Cac + "ParentDocumentLineReference",
                    new XElement(Cbc + "LineID", "1"),
                    new XElement(Cac + "DocumentReference",
                        new XElement(Cbc + "ID", parentId),
                        new XElement(Cbc + "UUID", new XAttribute("schemeName", "CUFE-SHA384"), cufe),
                        new XElement(Cbc + "IssueDate", issueDate),
                        new XElement(Cbc + "DocumentType", "ApplicationResponse"),
                        new XElement(Cac + "Attachment",
                            new XElement(Cac + "ExternalReference",
                                new XElement(Cbc + "MimeCode", "text/xml"),
                                new XElement(Cbc + "EncodingCode", "UTF-8"),
                                new XElement(Cbc + "Description", new XCData(WithoutDeclaration(applicationResponseXml))))),
                        new XElement(Cac + "ResultOfVerification",
                            new XElement(Cbc + "ValidatorID", "Unidad Especial Dirección de Impuestos y Aduanas Nacionales"),
                            new XElement(Cbc + "ValidationResultCode", "02"),
                            new XElement(Cbc + "ValidationDate", validationDate),
                            new XElement(Cbc + "ValidationTime", validationTime)))));

            return "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"no\"?>" + attached.ToString(SaveOptions.DisableFormatting);
        }

        // Asunto del correo al adquirente (numeral 9.1): NIT; nombre del facturador; número del documento; código del tipo de documento;
        // nombre comercial del facturador. Sale del XML firmado.
        public static string BuildEmailSubject(string signedDocumentXml)
        {
            var root = XDocument.Parse(signedDocumentXml).Root ?? throw new InvalidOperationException("El documento firmado no tiene contenido.");
            var supplier = root.Element(Cac + "AccountingSupplierParty")?.Element(Cac + "Party");
            var tax = supplier?.Element(Cac + "PartyTaxScheme");
            var nit = tax?.Element(Cbc + "CompanyID")?.Value.Trim() ?? string.Empty;
            var legalName = tax?.Element(Cbc + "RegistrationName")?.Value.Trim() ?? string.Empty;
            var tradeName = supplier?.Element(Cac + "PartyName")?.Element(Cbc + "Name")?.Value.Trim();
            if (string.IsNullOrWhiteSpace(tradeName)) tradeName = legalName;
            var number = root.Element(Cbc + "ID")?.Value.Trim() ?? string.Empty;

            // Facturas: cbc:InvoiceTypeCode. Notas: los valores de la tabla de tipos de documento (91 crédito, 92 débito).
            var typeCode = root.Element(Cbc + "InvoiceTypeCode")?.Value.Trim();
            if (string.IsNullOrWhiteSpace(typeCode))
                typeCode = root.Name.LocalName switch { "CreditNote" => "91", "DebitNote" => "92", _ => "01" };

            return string.Join(";", nit, legalName, number, typeCode, tradeName);
        }

        // Solo los datos tributarios que pide el contenedor, copiados del documento.
        private static XElement? PartyTaxScheme(XElement root, string partyName)
        {
            var source = root.Element(Cac + partyName)?.Element(Cac + "Party")?.Element(Cac + "PartyTaxScheme");
            if (source == null) return null;
            var result = new XElement(Cac + "PartyTaxScheme");
            foreach (var name in new[] { "RegistrationName", "CompanyID", "TaxLevelCode" })
            {
                var element = source.Element(Cbc + name);
                if (element != null) result.Add(new XElement(element));
            }
            var scheme = source.Element(Cac + "TaxScheme");
            if (scheme != null) result.Add(new XElement(scheme));
            return result;
        }

        private static string WithoutDeclaration(string xml)
        {
            var trimmed = xml.TrimStart('﻿', ' ', '\r', '\n', '\t');
            if (!trimmed.StartsWith("<?xml", StringComparison.Ordinal)) return trimmed;
            var end = trimmed.IndexOf("?>", StringComparison.Ordinal);
            return end < 0 ? trimmed : trimmed[(end + 2)..].TrimStart();
        }
    }
}
