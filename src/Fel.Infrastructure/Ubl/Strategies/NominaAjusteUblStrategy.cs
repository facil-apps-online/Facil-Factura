using System.Xml.Linq;
using Fel.Core.Models;
using Fel.Core.Interfaces;

namespace Fel.Infrastructure.Ubl.Strategies
{
    // Nota de Ajuste de Documento Soporte de Pago de Nómina Electrónica (DianCode 103), rama
    // "Eliminar" (TipoNota=2) — anula un documento de nómina ya transmitido por error. La rama
    // "Reemplazar" (TipoNota=1, corrección) no está implementada todavía.
    public class NominaAjusteUblStrategy
    {
        private static readonly XNamespace nomina = "dian:gov:co:facturaelectronica:NominaIndividualDeAjuste";
        private static readonly XNamespace ext = "urn:oasis:names:specification:ubl:schema:xsd:CommonExtensionComponents-2";
        private static readonly XNamespace xades = "http://uri.etsi.org/01903/v1.3.2#";
        private static readonly XNamespace xades141 = "http://uri.etsi.org/01903/v1.4.1#";
        private static readonly XNamespace ds = "http://www.w3.org/2000/09/xmldsig#";

        private readonly ICryptoService _cryptoService;

        public NominaAjusteUblStrategy(ICryptoService cryptoService)
        {
            _cryptoService = cryptoService;
        }

        public XElement GenerateEliminarXml(UblPayrollVoidData data, string cune)
        {
            return new XElement(nomina + "NominaIndividualDeAjuste",
                new XAttribute(XNamespace.Xmlns + "ext", ext),
                new XAttribute(XNamespace.Xmlns + "xades", xades),
                new XAttribute(XNamespace.Xmlns + "xades141", xades141),
                new XAttribute(XNamespace.Xmlns + "ds", ds),
                new XAttribute(XNamespace.Xmlns + "xsi", "http://www.w3.org/2001/XMLSchema-instance"),

                new XElement(ext + "UBLExtensions",
                    new XElement(ext + "UBLExtension",
                        new XElement(ext + "ExtensionContent",
                            new XElement("SignaturePlaceholder")
                        )
                    )
                ),

                new XElement("TipoNota", "2"), // 2 = Eliminar

                new XElement("Eliminar",
                    new XElement("EliminandoPredecesor",
                        new XAttribute("NumeroPred", data.PredecessorNumber),
                        new XAttribute("CUNEPred", data.PredecessorCune),
                        new XAttribute("FechaGenPred", NominaUblStrategy.D(data.PredecessorIssueDate))
                    ),
                    new XElement("NumeroSecuenciaXML",
                        new XAttribute("Prefijo", data.Prefix),
                        new XAttribute("Consecutivo", data.DocumentNumber),
                        new XAttribute("Numero", $"{data.Prefix}{data.DocumentNumber}")
                    ),
                    new XElement("LugarGeneracionXML",
                        new XAttribute("Pais", data.Employer.CountryCode),
                        new XAttribute("DepartamentoEstado", data.Employer.DepartmentCode),
                        new XAttribute("MunicipioCiudad", data.Employer.CityCode),
                        new XAttribute("Idioma", "es")
                    ),
                    new XElement("ProveedorXML",
                        new XAttribute("RazonSocial", data.Employer.CompanyName),
                        new XAttribute("PrimerApellido", ""),
                        new XAttribute("SegundoApellido", ""),
                        new XAttribute("PrimerNombre", ""),
                        new XAttribute("OtrosNombres", ""),
                        new XAttribute("NIT", data.Employer.TaxId),
                        new XAttribute("DV", data.Employer.VerificationDigit),
                        new XAttribute("SoftwareID", data.SoftwareId),
                        new XAttribute("SoftwareSC", _cryptoService.GenerateCufeSha384($"{data.SoftwareId}{data.SoftwarePin}"))
                    ),
                    new XElement("CodigoQR", NominaUblStrategy.BuildQrUrl(data.Environment, cune)),
                    new XElement("InformacionGeneral",
                        new XAttribute("Version", "V1.0: Documento Soporte de Pago de Nómina Electrónica"),
                        new XAttribute("Ambiente", data.Environment),
                        new XAttribute("TipoXML", "103"),
                        new XAttribute("CUNE", cune),
                        new XAttribute("EncripCUNE", "CUNE-SHA384"),
                        new XAttribute("FechaGen", NominaUblStrategy.D(data.IssueDate)),
                        new XAttribute("HoraGen", data.IssueTime.ToString("HH:mm:ss"))
                    ),
                    new XElement("Notas", data.Notes),
                    new XElement("Empleador",
                        new XAttribute("RazonSocial", data.Employer.CompanyName),
                        new XAttribute("PrimerApellido", ""),
                        new XAttribute("SegundoApellido", ""),
                        new XAttribute("PrimerNombre", ""),
                        new XAttribute("OtrosNombres", ""),
                        new XAttribute("NIT", data.Employer.TaxId),
                        new XAttribute("DV", data.Employer.VerificationDigit),
                        new XAttribute("Pais", data.Employer.CountryCode),
                        new XAttribute("DepartamentoEstado", data.Employer.DepartmentCode),
                        new XAttribute("MunicipioCiudad", data.Employer.CityCode),
                        new XAttribute("Direccion", data.Employer.Address)
                    )
                )
            );
        }
    }
}
