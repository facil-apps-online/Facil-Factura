using System.Xml.Linq;
using Fel.Core.Models;
using Fel.Core.Interfaces;

namespace Fel.Infrastructure.Ubl.Strategies
{
    public class CreditNoteUblStrategy : BaseUblStrategy
    {
        private static readonly XNamespace ubl = "urn:oasis:names:specification:ubl:schema:xsd:CreditNote-2";

        public CreditNoteUblStrategy(ICryptoService cryptoService) : base(cryptoService) { }

        public override XElement GenerateXml(UblInvoiceData data, string cufe)
        {
            // Nota referenciada vs. no referenciada: el bloque BillingReference (la factura que se
            // está afectando) solo aplica cuando de verdad hay una factura puntual de por medio. Si
            // no hay CUFE, la DIAN sí permite notas crédito no atadas a una factura electrónica
            // específica, y ese bloque no debe aparecer con valores vacíos.
            bool isReferenced = !string.IsNullOrWhiteSpace(data.BillingReferenceCufe);

            var creditNote = new XElement(ubl + "CreditNote",
                new XAttribute(XNamespace.Xmlns + "cac", cac),
                new XAttribute(XNamespace.Xmlns + "cbc", cbc),
                new XAttribute(XNamespace.Xmlns + "ext", ext),
                new XAttribute(XNamespace.Xmlns + "sts", sts),
                new XAttribute(XNamespace.Xmlns + "xades", xades),
                new XAttribute(XNamespace.Xmlns + "xades141", xades141),
                new XAttribute(XNamespace.Xmlns + "ds", ds),
                new XAttribute(XNamespace.Xmlns + "xsi", "http://www.w3.org/2001/XMLSchema-instance"),
                new XAttribute(XNamespace.Get("http://www.w3.org/2001/XMLSchema-instance") + "schemaLocation", "urn:oasis:names:specification:ubl:schema:xsd:CreditNote-2 http://docs.oasis-open.org/ubl/os-UBL-2.1/xsd/maindoc/UBL-CreditNote-2.1.xsd"),

                BuildExtensions(data, cufe),
                
                new XElement(cbc + "UBLVersionID", "UBL 2.1"),
                new XElement(cbc + "CustomizationID", data.OperationType),
                new XElement(cbc + "ProfileID", "DIAN 2.1: Nota Crédito de Factura Electrónica de Venta"),
                new XElement(cbc + "ProfileExecutionID", data.Environment), 
                new XElement(cbc + "ID", $"{data.Prefix}{data.DocumentNumber}"),
                new XElement(cbc + "UUID", new XAttribute("schemeID", data.Environment), new XAttribute("schemeName", "CUDE-SHA384"), cufe),
                new XElement(cbc + "IssueDate", DianTimeFormat.IssueDate(data.IssueDate)),
                new XElement(cbc + "IssueTime", DianTimeFormat.IssueTime(data.IssueTime)),
                new XElement(cbc + "CreditNoteTypeCode", data.DianCode),
                new XElement(cbc + "DocumentCurrencyCode", new XAttribute("listAgencyID", "6"), new XAttribute("listAgencyName", "United Nations Economic Commission for Europe"), new XAttribute("listID", "ISO 4217 Alpha"), data.Currency),
                new XElement(cbc + "LineCountNumeric", data.Lines.Count.ToString()),

                // DiscrepancyResponse: siempre presente (es el motivo del ajuste), referencie o no
                // una factura puntual. Cuando es referenciada, ReferenceID repite el número de esa
                // factura; cuando no, no hay documento que referenciar en este campo.
                new XElement(cac + "DiscrepancyResponse",
                    new XElement(cbc + "ReferenceID", isReferenced ? data.BillingReferenceDocumentNumber : ""),
                    new XElement(cbc + "ResponseCode", data.DiscrepancyResponseCode ?? "2"),
                    new XElement(cbc + "Description", data.DiscrepancyDescription ?? "Anulación parcial")
                ),

                // BillingReference: solo para notas referenciadas — con nota no referenciada, la
                // DIAN no espera este bloque (no hay una InvoiceDocumentReference válida que armar).
                isReferenced
                    ? new XElement(cac + "BillingReference",
                        new XElement(cac + "InvoiceDocumentReference",
                            new XElement(cbc + "ID", data.BillingReferenceDocumentNumber),
                            new XElement(cbc + "UUID", new XAttribute("schemeName", "CUFE-SHA384"), data.BillingReferenceCufe),
                            new XElement(cbc + "IssueDate", data.BillingReferenceDate?.ToString("yyyy-MM-dd") ?? DianTimeFormat.IssueDate(data.IssueDate))
                        )
                      )
                    : null,

                BuildAccountingSupplierParty(data),
                BuildAccountingCustomerParty(data),
                BuildPaymentMeans(data),
                BuildTaxTotals(data),
                BuildLegalMonetaryTotal(data)
            );

            for (int i = 0; i < data.Lines.Count; i++)
            {
                creditNote.Add(BuildLine("CreditNoteLine", "CreditedQuantity", data.Lines[i], data.Currency, i + 1));
            }

            return creditNote;
        }
    }
}
