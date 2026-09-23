using System.Xml.Linq;
using Fel.Core.Models;
using Fel.Core.Interfaces;

namespace Fel.Infrastructure.Ubl.Strategies
{
    public class DocumentoSoporteUblStrategy : BaseUblStrategy
    {
        private static readonly XNamespace ubl = "urn:oasis:names:specification:ubl:schema:xsd:Invoice-2";

        public DocumentoSoporteUblStrategy(ICryptoService cryptoService) : base(cryptoService) { }

        public override XElement GenerateXml(UblInvoiceData data, string cufe)
        {
            var invoice = new XElement(ubl + "Invoice",
                new XAttribute(XNamespace.Xmlns + "cac", cac),
                new XAttribute(XNamespace.Xmlns + "cbc", cbc),
                new XAttribute(XNamespace.Xmlns + "ext", ext),
                new XAttribute(XNamespace.Xmlns + "sts", sts),
                new XAttribute(XNamespace.Xmlns + "xades", xades),
                new XAttribute(XNamespace.Xmlns + "xades141", xades141),
                new XAttribute(XNamespace.Xmlns + "ds", ds),
                new XAttribute(XNamespace.Xmlns + "xsi", "http://www.w3.org/2001/XMLSchema-instance"),
                new XAttribute(XNamespace.Get("http://www.w3.org/2001/XMLSchema-instance") + "schemaLocation", "urn:oasis:names:specification:ubl:schema:xsd:Invoice-2 http://docs.oasis-open.org/ubl/os-UBL-2.1/xsd/maindoc/UBL-Invoice-2.1.xsd"),

                BuildExtensions(data, cufe),
                
                new XElement(cbc + "UBLVersionID", "UBL 2.1"),
                new XElement(cbc + "CustomizationID", data.OperationType),
                // Literal exacto exigido por la DIAN (Anexo Técnico v1.1, numeral DSAD03) — nótese
                // "documento soporte" en minúscula, la validación es de coincidencia exacta.
                new XElement(cbc + "ProfileID", "DIAN 2.1: documento soporte en adquisiciones efectuadas a no obligados a facturar."),
                new XElement(cbc + "ProfileExecutionID", data.Environment), 
                new XElement(cbc + "ID", $"{data.Prefix}{data.DocumentNumber}"),
                new XElement(cbc + "UUID", new XAttribute("schemeID", data.Environment), new XAttribute("schemeName", "CUDS-SHA384"), cufe),
                new XElement(cbc + "IssueDate", DianTimeFormat.IssueDate(data.IssueDate)),
                new XElement(cbc + "IssueTime", DianTimeFormat.IssueTime(data.IssueTime)),
                new XElement(cbc + "InvoiceTypeCode", data.DianCode),
                new XElement(cbc + "DocumentCurrencyCode", new XAttribute("listAgencyID", "6"), new XAttribute("listAgencyName", "United Nations Economic Commission for Europe"), new XAttribute("listID", "ISO 4217 Alpha"), data.Currency),
                new XElement(cbc + "LineCountNumeric", data.Lines.Count.ToString()),

                // En el Documento Soporte, el "Supplier" es el Vendedor (No Obligado) y el "Customer" es el Adquirente (Quien emite el doc).
                BuildAccountingSupplierParty(data),
                BuildAccountingCustomerParty(data),
                BuildTaxTotals(data),
                BuildLegalMonetaryTotal(data)
            );

            for (int i = 0; i < data.Lines.Count; i++)
            {
                invoice.Add(BuildLine("InvoiceLine", "InvoicedQuantity", data.Lines[i], data.Currency, i + 1));
            }

            return invoice;
        }
    }
}
