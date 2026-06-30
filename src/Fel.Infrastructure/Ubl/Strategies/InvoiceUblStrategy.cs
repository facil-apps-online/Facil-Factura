using System.Xml.Linq;
using Fel.Core.Models;
using Fel.Core.Interfaces;

namespace Fel.Infrastructure.Ubl.Strategies
{
    public class InvoiceUblStrategy : BaseUblStrategy
    {
        private static readonly XNamespace ubl = "urn:oasis:names:specification:ubl:schema:xsd:Invoice-2";

        public InvoiceUblStrategy(ICryptoService cryptoService) : base(cryptoService) { }

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

                BuildExtensions(data),
                
                new XElement(cbc + "UBLVersionID", "UBL 2.1"),
                new XElement(cbc + "CustomizationID", data.OperationType),
                new XElement(cbc + "ProfileID", "DIAN 2.1: Factura Electrónica de Venta"),
                new XElement(cbc + "ProfileExecutionID", data.Environment), 
                new XElement(cbc + "ID", $"{data.Prefix}{data.DocumentNumber}"),
                new XElement(cbc + "UUID", new XAttribute("schemeID", data.Environment), new XAttribute("schemeName", "CUFE-SHA384"), cufe),
                new XElement(cbc + "IssueDate", data.IssueDate.ToString("yyyy-MM-dd")),
                new XElement(cbc + "IssueTime", data.IssueTime.ToString("HH:mm:sszzz")),
                new XElement(cbc + "InvoiceTypeCode", data.DianCode),
                new XElement(cbc + "DocumentCurrencyCode", data.Currency),
                new XElement(cbc + "LineCountNumeric", data.Lines.Count.ToString()),

                BuildAccountingSupplierParty(data),
                BuildAccountingCustomerParty(data),
                BuildTaxTotals(data),
                BuildLegalMonetaryTotal(data)
            );

            foreach (var line in data.Lines)
            {
                invoice.Add(BuildLine("InvoiceLine", "InvoicedQuantity", line, data.Currency));
            }

            return invoice;
        }
    }
}
