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
        // Namespace real confirmado contra el targetNamespace del XSD oficial (DIAN_UBL_Structures.xsd)
        // y contra el ejemplo real de la DIAN (Generica.xml) — el valor anterior
        // ("http://www.dian.gov.co/contratos/facturaelectronica/v1/Structures") no coincide con
        // ninguno de los dos y hacia que sts:DianExtensions (SoftwareProvider, SoftwareSecurityCode,
        // AuthorizationProvider, QRCode) fuera invisible para el XPath namespace-aware de la DIAN.
        private static readonly XNamespace sts = "dian:gov:co:facturaelectronica:Structures-2-1";
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
            // Documento Equivalente Electrónico (DianCode 20 = tiquete POS) y Documento Soporte
            // (05/95) usan su propia fórmula de identificador, distinta a la de factura.
            string cufeOrCude = data.DianCode switch
            {
                "20" => CalculateEquivalentDocumentCufe(data),
                "05" or "95" => CalculateCuds(data),
                "91" or "92" => CalculateCude(data),
                _ => CalculateCufe(data)
            };

            Strategies.BaseUblStrategy strategy;

            switch (data.DianCode)
            {
                case "91": // Nota Crédito
                    strategy = new Strategies.CreditNoteUblStrategy(_cryptoService);
                    break;
                case "92": // Nota Débito
                    strategy = new Strategies.DebitNoteUblStrategy(_cryptoService);
                    break;
                case "05": // Documento Soporte (emisión) — va en <Invoice>
                    strategy = new Strategies.DocumentoSoporteUblStrategy(_cryptoService);
                    break;
                case "95": // Nota de Ajuste Documento Soporte — va en <CreditNote>
                    strategy = new Strategies.DocumentoSoporteAjusteUblStrategy(_cryptoService);
                    break;
                // Nómina (102/103) no pasa por aquí: no es UBL Invoice-family, tiene su propio
                // esquema y sus propios métodos (GeneratePayrollXml/GeneratePayrollVoidXml).
                default:
                    // 01, 02, 03, 04, 20... (Facturas y equivalentes)
                    strategy = new Strategies.InvoiceUblStrategy(_cryptoService);
                    break;
            }

            var xml = strategy.GenerateXml(data, cufeOrCude);
            return xml.ToString();
        }

        public string CalculateCufe(UblInvoiceData data)
        {
            // NumFac + FecFac + HorFac + ValFac + CodImp1 + ValImp1 + CodImp2 + ValImp2 + CodImp3 + ValImp3 + ValImp + ValTol + NitOFE + NumAdq + ClaveTec + Ambiente
            
            var valFac = data.LineExtensionAmount.ToString("0.00").Replace(",", ".");
            // ValTol/ValTot = PayableAmount (Anexo Técnico 11.2/11.4): con descuentos o cargos a nivel de factura
            // difiere de TaxInclusiveAmount.
            var valTol = data.PayableAmount.ToString("0.00").Replace(",", ".");
            
            var iva = data.Taxes.FirstOrDefault(t => t.TaxId == "01");
            var inc = data.Taxes.FirstOrDefault(t => t.TaxId == "04");
            var ica = data.Taxes.FirstOrDefault(t => t.TaxId == "03");

            var codImp1 = "01";
            var valImp1 = iva?.TaxAmount.ToString("0.00").Replace(",", ".") ?? "0.00";
            
            var codImp2 = "04";
            var valImp2 = inc?.TaxAmount.ToString("0.00").Replace(",", ".") ?? "0.00";
            
            var codImp3 = "03";
            var valImp3 = ica?.TaxAmount.ToString("0.00").Replace(",", ".") ?? "0.00";

            // FecFac/HorFac en hora de Bogotá (-05:00), no UTC — Anexo Técnico, regla FAD10 ("Debe
            // ser informada la hora en una zona horaria -5"). Mismo helper que usa la salida XML
            // (cbc:IssueDate/cbc:IssueTime en cada estrategia), para que CUFE y XML siempre coincidan.
            var issueDate = DianTimeFormat.IssueDate(data.IssueDate);
            var issueTime = DianTimeFormat.IssueTime(data.IssueTime);

            // SHA-384(NumFac+FecFac+HorFac+ValFac+CodImp1+ValImp1+CodImp2+ValImp2+CodImp3+ValImp3+
            // ValTot+NitOFE+NumAdq+ClTec+TipoAmbiente) — Anexo Técnico numeral 11.2. Antes traía un
            // término extra ("totalImpuestos") que no existe en la fórmula oficial, antes de ValTot
            // — la DIAN rechazaba con "Valor del CUFE no está calculado correctamente".
            string cufeString = $"{data.Prefix}{data.DocumentNumber}{issueDate}{issueTime}{valFac}{codImp1}{valImp1}{codImp2}{valImp2}{codImp3}{valImp3}{valTol}{data.Issuer.TaxId}{data.Customer.TaxId}{data.TechnicalKey}{data.Environment}";

            return _cryptoService.GenerateCufeSha384(cufeString);
        }

        // CUDE de Nota Crédito/Débito — Anexo Técnico numeral 11.4.3/11.4.4 (NC) y 11.4.5/11.4.6
        // (ND): misma fórmula que CalculateCufe, pero usa el Software-PIN del Client en vez de la
        // Clave Técnica de la resolución (mismo patrón que CalculateEquivalentDocumentCufe/
        // CalculateCuds para Documento Equivalente/Documento Soporte) — las notas no tienen
        // resolución propia, así que no hay Clave Técnica que usar.
        public string CalculateCude(UblInvoiceData data)
        {
            var valFac = data.LineExtensionAmount.ToString("0.00").Replace(",", ".");
            // ValTol/ValTot = PayableAmount (Anexo Técnico 11.2/11.4): con descuentos o cargos a nivel de factura
            // difiere de TaxInclusiveAmount.
            var valTol = data.PayableAmount.ToString("0.00").Replace(",", ".");

            var iva = data.Taxes.FirstOrDefault(t => t.TaxId == "01");
            var inc = data.Taxes.FirstOrDefault(t => t.TaxId == "04");
            var ica = data.Taxes.FirstOrDefault(t => t.TaxId == "03");

            var valImp1 = iva?.TaxAmount.ToString("0.00").Replace(",", ".") ?? "0.00";
            var valImp2 = inc?.TaxAmount.ToString("0.00").Replace(",", ".") ?? "0.00";
            var valImp3 = ica?.TaxAmount.ToString("0.00").Replace(",", ".") ?? "0.00";

            var issueDate = DianTimeFormat.IssueDate(data.IssueDate);
            var issueTime = DianTimeFormat.IssueTime(data.IssueTime);

            string cudeString = $"{data.Prefix}{data.DocumentNumber}{issueDate}{issueTime}{valFac}01{valImp1}04{valImp2}03{valImp3}{valTol}{data.Issuer.TaxId}{data.Customer.TaxId}{data.SoftwarePin}{data.Environment}";
            return _cryptoService.GenerateCufeSha384(cudeString);
        }

        // Contenido del código QR de la representación gráfica impresa — Anexo Técnico v1.9,
        // numeral 11.7, formato confirmado contra una respuesta real de un proveedor tecnológico
        // ya certificado (Dataico) en producción: NumFac/FecFac/HorFac/NitFac/DocAdq/ValFac/ValIva/
        // ValOtroIm/ValTolFac/CUFE/QRCode=<url>. Antes la plantilla de impresión solo tenía el CUFE
        // disponible y codificaba únicamente eso en el QR, sin el resto de los datos que exige el
        // anexo.
        public string BuildGraphicQrContent(UblInvoiceData data, string cufe)
        {
            var qrBaseUrl = data.Environment == "1"
                ? "https://catalogo-vpfe.dian.gov.co/document/searchqr?documentkey="
                : "https://catalogo-vpfe-hab.dian.gov.co/document/searchqr?documentkey=";

            var valIva = data.Taxes.Where(t => t.TaxId == "01").Sum(t => t.TaxAmount);
            var valOtroIm = data.Taxes.Where(t => t.TaxId != "01").Sum(t => t.TaxAmount);

            var lines = new[]
            {
                $"NumFac={data.Prefix}{data.DocumentNumber}",
                $"FecFac={DianTimeFormat.IssueDate(data.IssueDate)}",
                $"HorFac={DianTimeFormat.IssueTime(data.IssueTime)}",
                $"NitFac={data.Issuer.TaxId}",
                $"DocAdq={data.Customer.TaxId}",
                $"ValFac={data.LineExtensionAmount.ToString("0.00").Replace(",", ".")}",
                $"ValIva={valIva.ToString("0.00").Replace(",", ".")}",
                $"ValOtroIm={valOtroIm.ToString("0.00").Replace(",", ".")}",
                $"ValTolFac={data.PayableAmount.ToString("0.00").Replace(",", ".")}",
                $"CUFE={cufe}",
                $"QRCode={qrBaseUrl}{cufe}"
            };
            return string.Join("\n", lines);
        }

        // CUFE/CUDE de Documento Equivalente Electrónico — Anexo Técnico v1.0 (Resolución 000165 de
        // 2023), numeral 14.1: SHA-384(NumFac+FecFac+HorFac+ValFac+CodImp1+ValImp1+CodImp2+ValImp2+
        // CodImp3+ValImp3+ValTot+NitOFE+NumAdq+Software-PIN+TipoAmbiente). A diferencia de CalculateCufe:
        // no lleva el término de total de impuestos, y usa el Software-PIN del Client en vez de la
        // Clave Técnica de la resolución.
        public string CalculateEquivalentDocumentCufe(UblInvoiceData data)
        {
            var valFac = data.LineExtensionAmount.ToString("0.00").Replace(",", ".");
            var valTot = data.TaxInclusiveAmount.ToString("0.00").Replace(",", ".");

            var iva = data.Taxes.FirstOrDefault(t => t.TaxId == "01");
            var inc = data.Taxes.FirstOrDefault(t => t.TaxId == "04");
            var ica = data.Taxes.FirstOrDefault(t => t.TaxId == "03");

            var valImp1 = iva?.TaxAmount.ToString("0.00").Replace(",", ".") ?? "0.00";
            var valImp2 = inc?.TaxAmount.ToString("0.00").Replace(",", ".") ?? "0.00";
            var valImp3 = ica?.TaxAmount.ToString("0.00").Replace(",", ".") ?? "0.00";

            var issueDate = DianTimeFormat.IssueDate(data.IssueDate);
            var issueTime = DianTimeFormat.IssueTime(data.IssueTime);

            string cufeString = $"{data.Prefix}{data.DocumentNumber}{issueDate}{issueTime}{valFac}01{valImp1}04{valImp2}03{valImp3}{valTot}{data.Issuer.TaxId}{data.Customer.TaxId}{data.SoftwarePin}{data.Environment}";
            return _cryptoService.GenerateCufeSha384(cufeString);
        }

        // CUDS — Anexo Técnico Documento Soporte v1.1, numeral 14.1.1.2:
        // SHA-384(NumDS+FecDS+HorDS+ValDS+CodImp(01)+ValImp+ValTot+NumSNO+NitABS+Software-PIN+TipoAmbiente).
        // A diferencia de CalculateCufe: solo IVA (no INC/ICA), y NumSNO/NitABS llegan en
        // data.Issuer/data.Customer porque en Documento Soporte esos roles están invertidos
        // (Issuer=Vendedor No Obligado, Customer=nuestro Client como Adquirente) — ver
        // DianDocumentMapper.BuildSupportDocumentDataFromRequest.
        public string CalculateCuds(UblInvoiceData data)
        {
            var valDs = data.LineExtensionAmount.ToString("0.00").Replace(",", ".");
            var valTot = data.TaxInclusiveAmount.ToString("0.00").Replace(",", ".");
            var iva = data.Taxes.FirstOrDefault(t => t.TaxId == "01");
            var valImp = iva?.TaxAmount.ToString("0.00").Replace(",", ".") ?? "0.00";

            var issueDate = DianTimeFormat.IssueDate(data.IssueDate);
            var issueTime = DianTimeFormat.IssueTime(data.IssueTime);

            string cudsString = $"{data.Prefix}{data.DocumentNumber}{issueDate}{issueTime}{valDs}01{valImp}{valTot}{data.Issuer.TaxId}{data.Customer.TaxId}{data.SoftwarePin}{data.Environment}";
            return _cryptoService.GenerateCufeSha384(cudsString);
        }

        public string GenerateEventXml(UblEventData data, string cude)
        {
            var strategy = new Strategies.ApplicationResponseUblStrategy(_cryptoService);
            return strategy.GenerateXml(data, cude).ToString();
        }

        // CUDE del evento — Anexo Técnico v1.9, numeral 11.5:
        // SHA-384(NumDE+FecEmi+HorEmi+NitFE+DocAdq+ResponseCode+ID+DocumentTypeCode+SoftwarePin).
        // NitFE = quien genera el evento (nuestro Client); DocAdq = quien lo recibe (el emisor
        // original de la factura referenciada); ID/DocumentTypeCode son del documento referenciado.
        public string CalculateEventCude(UblEventData data)
        {
            var fecEmi = data.IssueDate.ToString("yyyy-MM-dd");
            var horEmi = data.IssueTime.ToString("HH:mm:sszzz");

            var cudeString = $"{data.DocumentNumber}{fecEmi}{horEmi}{data.SenderTaxId}{data.ReceiverTaxId}{data.ResponseCode}{data.ReferencedDocumentId}{data.ReferencedDocumentTypeCode}{data.SoftwarePin}";
            return _cryptoService.GenerateCufeSha384(cudeString);
        }

        public string GeneratePayrollXml(UblPayrollData data, string cune)
        {
            var strategy = new Strategies.NominaUblStrategy(_cryptoService);
            return strategy.GenerateXml(data, cune).ToString();
        }

        // CUNE — Anexo Técnico Documento Soporte de Pago de Nómina Electrónica v1.0, numeral 8.1.1.1:
        // SHA-384(NumNE + FecNE + HorNE + ValDev + ValDed + ValTolNE + NitNE + DocEmp + TipoXML +
        // SoftwarePin + TipAmb). A diferencia del CUFE, los decimales van TRUNCADOS a 2 dígitos, no
        // redondeados — verificado contra el ejemplo oficial del anexo (docs/dian-nomina/).
        public string CalculateCune(UblPayrollData data)
        {
            var numNe = $"{data.Prefix}{data.DocumentNumber}";
            var fecNe = data.IssueDate.ToString("yyyy-MM-dd");
            var horNe = data.IssueTime.ToString("HH:mm:sszzz");
            var valDev = TruncateTwoDecimals(data.EarningsTotal);
            var valDed = TruncateTwoDecimals(data.DeductionsTotal);
            var valTol = TruncateTwoDecimals(data.PayableTotal);
            var nitNe = data.Employer.TaxId;
            var docEmp = data.Worker.IdentificationNumber;

            var cuneString = $"{numNe}{fecNe}{horNe}{valDev}{valDed}{valTol}{nitNe}{docEmp}{data.DianCode}{data.SoftwarePin}{data.Environment}";
            return _cryptoService.GenerateCufeSha384(cuneString);
        }

        public string GeneratePayrollVoidXml(UblPayrollVoidData data, string cune)
        {
            var strategy = new Strategies.NominaAjusteUblStrategy(_cryptoService);
            return strategy.GenerateEliminarXml(data, cune).ToString();
        }

        // Rama "Eliminar": el anexo indica ValDev/ValDed/ValTol/DocEmp en 0 (no hay detalle de
        // nómina en una anulación), TipoXML fijo en "103".
        public string CalculateVoidCune(UblPayrollVoidData data)
        {
            var numNe = $"{data.Prefix}{data.DocumentNumber}";
            var fecNe = data.IssueDate.ToString("yyyy-MM-dd");
            var horNe = data.IssueTime.ToString("HH:mm:sszzz");
            var nitNe = data.Employer.TaxId;

            var cuneString = $"{numNe}{fecNe}{horNe}0.000.000.00{nitNe}0103{data.SoftwarePin}{data.Environment}";
            return _cryptoService.GenerateCufeSha384(cuneString);
        }

        private static string TruncateTwoDecimals(decimal value)
        {
            var truncated = Math.Truncate(value * 100) / 100;
            return truncated.ToString("0.00").Replace(",", ".");
        }
    }
}
