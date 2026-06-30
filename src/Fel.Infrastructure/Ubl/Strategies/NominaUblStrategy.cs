using System.Xml.Linq;
using Fel.Core.Models;
using Fel.Core.Interfaces;

namespace Fel.Infrastructure.Ubl.Strategies
{
    public class NominaUblStrategy : BaseUblStrategy
    {
        private static readonly XNamespace nomina = "dian:gov:co:facturaelectronica:NominaIndividual";

        public NominaUblStrategy(ICryptoService cryptoService) : base(cryptoService) { }

        public override XElement GenerateXml(UblInvoiceData data, string cune)
        {
            // Nómina Electrónica usa un esquema distinto a UBL estándar
            var nominaIndividual = new XElement(nomina + "NominaIndividual",
                new XAttribute(XNamespace.Xmlns + "ext", ext),
                new XAttribute(XNamespace.Xmlns + "xades", xades),
                new XAttribute(XNamespace.Xmlns + "xades141", xades141),
                new XAttribute(XNamespace.Xmlns + "ds", ds),

                new XElement(ext + "UBLExtensions",
                    new XElement(ext + "UBLExtension",
                        new XElement(ext + "ExtensionContent",
                            new XElement("SignaturePlaceholder")
                        )
                    )
                ),

                new XElement("Novedad", new XAttribute("CUNE", cune)),
                new XElement("Periodo", 
                    new XAttribute("FechaIngreso", "2024-01-01"),
                    new XAttribute("FechaLiquidacionInicio", data.IssueDate.ToString("yyyy-MM-01")),
                    new XAttribute("FechaLiquidacionFin", data.IssueDate.ToString("yyyy-MM-28")),
                    new XAttribute("TiempoLaborado", "30")
                ),
                new XElement("NumeroSecuenciaXML", new XAttribute("CodigoTrabajador", data.Customer.TaxId), new XAttribute("Prefijo", data.Prefix), new XAttribute("Consecutivo", data.DocumentNumber), new XAttribute("Numero", $"{data.Prefix}{data.DocumentNumber}")),
                new XElement("LugarGeneracionXML", new XAttribute("Pais", "CO"), new XAttribute("DepartamentoEstado", data.Issuer.DepartmentCode), new XAttribute("MunicipioCiudad", data.Issuer.CityCode), new XAttribute("Idioma", "es")),
                new XElement("InformacionGeneral", 
                    new XAttribute("Version", "V1.0: Documento Soporte de Pago de Nómina Electrónica"),
                    new XAttribute("Ambiente", data.Environment),
                    new XAttribute("TipoXML", data.DianCode)
                ),
                
                new XElement("Empleador",
                    new XAttribute("NIT", data.Issuer.TaxId),
                    new XAttribute("RazonSocial", data.Issuer.Name)
                ),
                new XElement("Trabajador",
                    new XAttribute("TipoDocumento", "13"),
                    new XAttribute("NumeroDocumento", data.Customer.TaxId),
                    new XAttribute("PrimerApellido", data.Customer.Name)
                ),
                
                new XElement("Pago",
                    new XAttribute("Forma", "1"),
                    new XAttribute("Metodo", "10")
                ),

                new XElement("Devengados",
                    new XElement("Basico", new XAttribute("DiasTrabajados", "30"), new XAttribute("SueldoTrabajado", data.LineExtensionAmount.ToString("0.00").Replace(",", ".")))
                ),
                new XElement("Deducciones"),
                new XElement("DevengadosTotal", data.LineExtensionAmount.ToString("0.00").Replace(",", ".")),
                new XElement("DeduccionesTotal", "0.00"),
                new XElement("ComprobanteTotal", data.LineExtensionAmount.ToString("0.00").Replace(",", "."))
            );

            return nominaIndividual;
        }
    }
}
