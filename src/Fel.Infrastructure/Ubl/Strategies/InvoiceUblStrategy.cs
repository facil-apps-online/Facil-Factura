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

                BuildExtensions(data, cufe),
                
                new XElement(cbc + "UBLVersionID", "UBL 2.1"),
                new XElement(cbc + "CustomizationID", data.OperationType),
                new XElement(cbc + "ProfileID", ResolveProfileId(data)),
                new XElement(cbc + "ProfileExecutionID", data.Environment), 
                new XElement(cbc + "ID", $"{data.Prefix}{data.DocumentNumber}"),
                new XElement(cbc + "UUID", new XAttribute("schemeID", data.Environment), new XAttribute("schemeName", "CUFE-SHA384"), cufe),
                new XElement(cbc + "IssueDate", DianTimeFormat.IssueDate(data.IssueDate)),
                new XElement(cbc + "IssueTime", DianTimeFormat.IssueTime(data.IssueTime)),
                new XElement(cbc + "InvoiceTypeCode", data.DianCode),
                // listAgencyID/listAgencyName/listID (confirmados contra el ejemplo oficial
                // Generica.xml de la Caja de Herramientas y contra un firmador Python de
                // referencia en producción activa) faltaban por completo.
                new XElement(cbc + "DocumentCurrencyCode", new XAttribute("listAgencyID", "6"), new XAttribute("listAgencyName", "United Nations Economic Commission for Europe"), new XAttribute("listID", "ISO 4217 Alpha"), data.Currency),
                new XElement(cbc + "LineCountNumeric", data.Lines.Count.ToString()),

                BuildAccountingSupplierParty(data),
                BuildAccountingCustomerParty(data),
                BuildPaymentMeans(data),
                BuildAllowanceCharges(data),
                BuildTaxTotals(data),
                BuildLegalMonetaryTotal(data, includeAllowances: true)
            );

            for (int i = 0; i < data.Lines.Count; i++)
            {
                invoice.Add(BuildLine("InvoiceLine", "InvoicedQuantity", data.Lines[i], data.Currency, i + 1));
            }

            return invoice;
        }

        // ProfileID literal exigido por la DIAN — la validación rechaza el documento si no coincide
        // exactamente. Cada subtipo de Documento Equivalente (InvoiceTypeCode/DianCode) tiene el
        // suyo propio (Anexo Técnico v1.0, secciones 8.2 a 8.11 — ver docs/dian-doc-equivalente/);
        // el de POS/SPD (DianCode 20) además distingue 601 (normal) vs 602 (en sitio).
        private static string ResolveProfileId(UblInvoiceData data) => data.DianCode switch
        {
            "20" => data.OperationType == "602" ? "DIAN 2.1: Documento Equivalente SPD – ON SITE" : "DIAN 2.1: Documento Equivalente SPD",
            "25" => "DIAN 2.1: Boleta de ingreso a cine",
            "27" => "DIAN 2.1: Boleta de ingreso a espectáculos públicos",
            "30" => "DIAN 2.1: Documento en juegos localizados - relación diaria de control de ventas",
            "35" => "DIAN 2.1: Documento Equivalente Tiquete de Transporte Terrestre de Pasajeros",
            "40" => "DIAN 2.1: Documento Equivalente para el Cobro de Peajes",
            "45" => "DIAN 2.1: Extracto Expedido por Sociedades Financieras y Fondos",
            "50" => "DIAN 2.1: Documento equivalente – Tiquete o boleto aéreo de pasajeros",
            "55" => "DIAN 2.1: Documento del comprobante de liquidación de operaciones expedido por Bolsa de Valores y operaciones de la bolsa agropecuaria y de otros commodities",
            "60" => "DIAN 2.1: Documento Expedido para los Servicios Públicos y Domiciliarios",
            _ => "DIAN 2.1: Factura Electrónica de Venta"
        };
    }
}
