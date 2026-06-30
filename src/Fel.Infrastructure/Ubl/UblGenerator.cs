using System;
using System.Linq;
using System.Xml.Linq;
using Fel.Core.Interfaces;
using Fel.Core.Models;

namespace Fel.Infrastructure.Ubl
{
    public class UblGenerator : IUblGenerator
    {
        private readonly ICryptoService _cryptoService;

        // Namespaces oficiales de la DIAN para UBL 2.1
        private static readonly XNamespace cac = "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2";
        private static readonly XNamespace cbc = "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2";
        private static readonly XNamespace ext = "urn:oasis:names:specification:ubl:schema:xsd:CommonExtensionComponents-2";
        private static readonly XNamespace sts = "http://www.dian.gov.co/contratos/facturaelectronica/v1/Structures";
        private static readonly XNamespace xades = "http://uri.etsi.org/01903/v1.3.2#";
        private static readonly XNamespace xades141 = "http://uri.etsi.org/01903/v1.4.1#";
        private static readonly XNamespace ds = "http://www.w3.org/2000/09/xmldsig#";
        private static readonly XNamespace ubl = "urn:oasis:names:specification:ubl:schema:xsd:Invoice-2";

        public UblGenerator(ICryptoService cryptoService)
        {
            _cryptoService = cryptoService;
        }

        public string GenerateInvoiceXml(UblInvoiceData data)
        {
            string cufeOrCude = CalculateCufe(data);
            
            Strategies.BaseUblStrategy strategy;

            switch (data.DianCode)
            {
                case "91": // Nota Crédito
                    strategy = new Strategies.CreditNoteUblStrategy(_cryptoService);
                    break;
                case "92": // Nota Débito
                    strategy = new Strategies.DebitNoteUblStrategy(_cryptoService);
                    break;
                case "05": // Documento Soporte
                case "95": // Nota de Ajuste Documento Soporte
                    strategy = new Strategies.DocumentoSoporteUblStrategy(_cryptoService);
                    break;
                case "102": // Nómina
                case "103": // Nota de Ajuste Nómina
                    strategy = new Strategies.NominaUblStrategy(_cryptoService);
                    break;
                default:
                    // 01, 02, 03, 04, 20... (Facturas y equivalentes)
                    strategy = new Strategies.InvoiceUblStrategy(_cryptoService);
                    break;
            }

            var xml = strategy.GenerateXml(data, cufeOrCude);
            return xml.ToString();
        }

        private string CalculateCufe(UblInvoiceData data)
        {
            // NumFac + FecFac + HorFac + ValFac + CodImp1 + ValImp1 + CodImp2 + ValImp2 + CodImp3 + ValImp3 + ValImp + ValTol + NitOFE + NumAdq + ClaveTec + Ambiente
            
            var valFac = data.LineExtensionAmount.ToString("0.00").Replace(",", ".");
            var valTol = data.TaxInclusiveAmount.ToString("0.00").Replace(",", ".");
            
            var iva = data.Taxes.FirstOrDefault(t => t.TaxId == "01");
            var inc = data.Taxes.FirstOrDefault(t => t.TaxId == "04");
            var ica = data.Taxes.FirstOrDefault(t => t.TaxId == "03");

            var codImp1 = "01";
            var valImp1 = iva?.TaxAmount.ToString("0.00").Replace(",", ".") ?? "0.00";
            
            var codImp2 = "04";
            var valImp2 = inc?.TaxAmount.ToString("0.00").Replace(",", ".") ?? "0.00";
            
            var codImp3 = "03";
            var valImp3 = ica?.TaxAmount.ToString("0.00").Replace(",", ".") ?? "0.00";

            var totalImpuestos = data.Taxes.Sum(t => t.TaxAmount).ToString("0.00").Replace(",", ".");

            var issueDate = data.IssueDate.ToString("yyyy-MM-dd");
            var issueTime = data.IssueTime.ToString("HH:mm:sszzz");

            string cufeString = $"{data.Prefix}{data.DocumentNumber}{issueDate}{issueTime}{valFac}{codImp1}{valImp1}{codImp2}{valImp2}{codImp3}{valImp3}{totalImpuestos}{valTol}{data.Issuer.TaxId}{data.Customer.TaxId}{data.TechnicalKey}{data.Environment}";

            return _cryptoService.GenerateCufeSha384(cufeString);
        }
    }
}
