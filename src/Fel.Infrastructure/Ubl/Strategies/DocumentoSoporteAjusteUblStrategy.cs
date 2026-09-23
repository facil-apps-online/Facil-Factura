using System.Xml.Linq;
using Fel.Core.Models;
using Fel.Core.Interfaces;

namespace Fel.Infrastructure.Ubl.Strategies
{
    // Nota de Ajuste al Documento Soporte (DianCode 95) — a diferencia de la emisión (Invoice), la
    // DIAN exige esta en <CreditNote> (Anexo Técnico v1.1, sección de Nota de Ajuste).
    public class DocumentoSoporteAjusteUblStrategy : BaseUblStrategy
    {
        private static readonly XNamespace ubl = "urn:oasis:names:specification:ubl:schema:xsd:CreditNote-2";

        public DocumentoSoporteAjusteUblStrategy(ICryptoService cryptoService) : base(cryptoService) { }

        public override XElement GenerateXml(UblInvoiceData data, string cuds)
        {
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

                BuildExtensions(data, cuds),

                new XElement(cbc + "UBLVersionID", "UBL 2.1"),
                new XElement(cbc + "CustomizationID", data.OperationType),
                // Literal exacto exigido por la DIAN (Anexo Técnico v1.1, numeral NSAD03).
                new XElement(cbc + "ProfileID", "DIAN 2.1: Nota de ajuste al documento soporte en adquisiciones efectuadas a sujetos no obligados a expedir factura o documento equivalente"),
                new XElement(cbc + "ProfileExecutionID", data.Environment),
                new XElement(cbc + "ID", $"{data.Prefix}{data.DocumentNumber}"),
                new XElement(cbc + "UUID", new XAttribute("schemeID", data.Environment), new XAttribute("schemeName", "CUDS-SHA384"), cuds),
                new XElement(cbc + "IssueDate", DianTimeFormat.IssueDate(data.IssueDate)),
                new XElement(cbc + "IssueTime", DianTimeFormat.IssueTime(data.IssueTime)),
                new XElement(cbc + "CreditNoteTypeCode", data.DianCode),
                new XElement(cbc + "DocumentCurrencyCode", new XAttribute("listAgencyID", "6"), new XAttribute("listAgencyName", "United Nations Economic Commission for Europe"), new XAttribute("listID", "ISO 4217 Alpha"), data.Currency),
                new XElement(cbc + "LineCountNumeric", data.Lines.Count.ToString()),

                new XElement(cac + "DiscrepancyResponse",
                    new XElement(cbc + "ReferenceID", isReferenced ? data.BillingReferenceDocumentNumber : ""),
                    new XElement(cbc + "ResponseCode", data.DiscrepancyResponseCode ?? "2"),
                    new XElement(cbc + "Description", data.DiscrepancyDescription ?? "Ajuste")
                ),

                isReferenced
                    ? new XElement(cac + "BillingReference",
                        new XElement(cac + "InvoiceDocumentReference",
                            new XElement(cbc + "ID", data.BillingReferenceDocumentNumber),
                            new XElement(cbc + "UUID", new XAttribute("schemeName", "CUDS-SHA384"), data.BillingReferenceCufe),
                            new XElement(cbc + "IssueDate", data.BillingReferenceDate?.ToString("yyyy-MM-dd") ?? DianTimeFormat.IssueDate(data.IssueDate))
                        )
                      )
                    : null,

                // Igual que en la emisión: Supplier = Vendedor No Obligado (SNO), Customer = nuestro
                // Client como Adquirente (ABS) — ver DocumentoSoporteUblStrategy.
                BuildAccountingSupplierParty(data),
                BuildAccountingCustomerParty(data),
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
