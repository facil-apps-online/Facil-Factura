using System.Xml.Linq;
using Fel.Core.Models;
using Fel.Core.Interfaces;

namespace Fel.Infrastructure.Ubl.Strategies
{
    public class DebitNoteUblStrategy : BaseUblStrategy
    {
        private static readonly XNamespace ubl = "urn:oasis:names:specification:ubl:schema:xsd:DebitNote-2";

        public DebitNoteUblStrategy(ICryptoService cryptoService) : base(cryptoService) { }

        public override XElement GenerateXml(UblInvoiceData data, string cufe)
        {
            var debitNote = new XElement(ubl + "DebitNote",
                new XAttribute(XNamespace.Xmlns + "cac", cac),
                new XAttribute(XNamespace.Xmlns + "cbc", cbc),
                new XAttribute(XNamespace.Xmlns + "ext", ext),
                new XAttribute(XNamespace.Xmlns + "sts", sts),
                new XAttribute(XNamespace.Xmlns + "xades", xades),
                new XAttribute(XNamespace.Xmlns + "xades141", xades141),
                new XAttribute(XNamespace.Xmlns + "ds", ds),
                new XAttribute(XNamespace.Xmlns + "xsi", "http://www.w3.org/2001/XMLSchema-instance"),
                new XAttribute(XNamespace.Get("http://www.w3.org/2001/XMLSchema-instance") + "schemaLocation", "urn:oasis:names:specification:ubl:schema:xsd:DebitNote-2 http://docs.oasis-open.org/ubl/os-UBL-2.1/xsd/maindoc/UBL-DebitNote-2.1.xsd"),

                BuildExtensions(data),
                
                new XElement(cbc + "UBLVersionID", "UBL 2.1"),
                new XElement(cbc + "CustomizationID", data.OperationType),
                new XElement(cbc + "ProfileID", "DIAN 2.1: Nota Débito de Factura Electrónica de Venta"),
                new XElement(cbc + "ProfileExecutionID", data.Environment), 
                new XElement(cbc + "ID", $"{data.Prefix}{data.DocumentNumber}"),
                new XElement(cbc + "UUID", new XAttribute("schemeID", data.Environment), new XAttribute("schemeName", "CUDE-SHA384"), cufe),
                new XElement(cbc + "IssueDate", data.IssueDate.ToString("yyyy-MM-dd")),
                new XElement(cbc + "IssueTime", data.IssueTime.ToString("HH:mm:sszzz")),
                new XElement(cbc + "DebitNoteTypeCode", data.DianCode),
                new XElement(cbc + "DocumentCurrencyCode", data.Currency),
                new XElement(cbc + "LineCountNumeric", data.Lines.Count.ToString()),

                // DiscrepancyResponse
                new XElement(cac + "DiscrepancyResponse",
                    new XElement(cbc + "ReferenceID", data.BillingReferenceDocumentNumber ?? ""),
                    new XElement(cbc + "ResponseCode", data.DiscrepancyResponseCode ?? "2"),
                    new XElement(cbc + "Description", data.DiscrepancyDescription ?? "Intereses")
                ),

                // BillingReference
                new XElement(cac + "BillingReference",
                    new XElement(cac + "InvoiceDocumentReference",
                        new XElement(cbc + "ID", data.BillingReferenceDocumentNumber ?? ""),
                        new XElement(cbc + "UUID", new XAttribute("schemeName", "CUFE-SHA384"), data.BillingReferenceCufe ?? ""),
                        new XElement(cbc + "IssueDate", data.BillingReferenceDate?.ToString("yyyy-MM-dd") ?? data.IssueDate.ToString("yyyy-MM-dd"))
                    )
                ),

                BuildAccountingSupplierParty(data),
                BuildAccountingCustomerParty(data),
                BuildTaxTotals(data),
                BuildLegalMonetaryTotal(data)
            );

            foreach (var line in data.Lines)
            {
                debitNote.Add(BuildLine("DebitNoteLine", "DebitedQuantity", line, data.Currency));
            }

            return debitNote;
        }
    }
}
