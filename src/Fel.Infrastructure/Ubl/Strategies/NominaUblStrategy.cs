using System.Globalization;
using System.Linq;
using System.Xml.Linq;
using Fel.Core.Models;
using Fel.Core.Interfaces;

namespace Fel.Infrastructure.Ubl.Strategies
{
    // Documento Soporte de Pago de Nómina Electrónica (DianCode 102). Esquema propio de la DIAN
    // (NO es UBL Invoice-family) — ver Anexo Técnico v1.0 en docs/dian-nomina/. Solo la sección de
    // firma usa UBL 2.1 (ext:UBLExtensions); el resto es la estructura NominaIndividual detallada
    // en el anexo, por eso esta clase no hereda de BaseUblStrategy (sus helpers son de UBL Invoice).
    public class NominaUblStrategy
    {
        private static readonly XNamespace nomina = "dian:gov:co:facturaelectronica:NominaIndividual";
        private static readonly XNamespace ext = "urn:oasis:names:specification:ubl:schema:xsd:CommonExtensionComponents-2";
        private static readonly XNamespace xades = "http://uri.etsi.org/01903/v1.3.2#";
        private static readonly XNamespace xades141 = "http://uri.etsi.org/01903/v1.4.1#";
        private static readonly XNamespace ds = "http://www.w3.org/2000/09/xmldsig#";

        private readonly ICryptoService _cryptoService;

        public NominaUblStrategy(ICryptoService cryptoService)
        {
            _cryptoService = cryptoService;
        }

        public XElement GenerateXml(UblPayrollData data, string cune)
        {
            var root = new XElement(nomina + "NominaIndividual",
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

                // Sin soporte de "novedades contractuales" todavía — se emite siempre sin novedad.
                new XElement("Novedad", new XAttribute("CUNENov", string.Empty), "false"),

                new XElement("Periodo",
                    new XAttribute("FechaIngreso", D(data.Period.HireDate)),
                    new XAttribute("FechaRetiro", D(data.Period.TerminationDate ?? data.Period.HireDate)),
                    new XAttribute("FechaLiquidacionInicio", D(data.Period.SettlementStart)),
                    new XAttribute("FechaLiquidacionFin", D(data.Period.SettlementEnd)),
                    new XAttribute("TiempoLaborado", data.Period.WorkedTime)
                ),

                new XElement("NumeroSecuenciaXML",
                    new XAttribute("CodigoTrabajador", data.Worker.WorkerCode),
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

                // ProveedorXML: modelo autogestionado — cada emisor hace su propia habilitación, así
                // que el proveedor tecnológico que declara el XML es el mismo empleador, igual que en
                // BaseUblStrategy.BuildExtensions para factura.
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

                new XElement("CodigoQR", BuildQrUrl(data.Environment, cune)),

                new XElement("InformacionGeneral",
                    new XAttribute("Version", "V1.0: Documento Soporte de Pago de Nómina Electrónica"),
                    new XAttribute("Ambiente", data.Environment),
                    new XAttribute("TipoXML", data.DianCode),
                    new XAttribute("CUNE", cune),
                    new XAttribute("EncripCUNE", "CUNE-SHA384"),
                    new XAttribute("FechaGen", D(data.IssueDate)),
                    new XAttribute("HoraGen", data.IssueTime.ToString("HH:mm:ss")),
                    new XAttribute("PeriodoNomina", "1"),
                    new XAttribute("TipoMoneda", "COP")
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
                ),

                new XElement("Trabajador",
                    new XAttribute("TipoTrabajador", data.Worker.WorkerTypeCode),
                    new XAttribute("SubTipoTrabajador", data.Worker.WorkerSubTypeCode),
                    new XAttribute("AltoRiesgoPension", B(data.Worker.HighPensionRisk)),
                    new XAttribute("TipoDocumento", data.Worker.IdentificationTypeCode),
                    new XAttribute("NumeroDocumento", data.Worker.IdentificationNumber),
                    new XAttribute("PrimerApellido", data.Worker.FirstSurname),
                    new XAttribute("SegundoApellido", data.Worker.SecondSurname),
                    new XAttribute("PrimerNombre", data.Worker.FirstName),
                    new XAttribute("OtrosNombres", data.Worker.OtherNames),
                    new XAttribute("LugarTrabajoPais", data.Worker.WorkplaceCountryCode),
                    new XAttribute("LugarTrabajoDepartamentoEstado", data.Worker.WorkplaceDepartmentCode),
                    new XAttribute("LugarTrabajoMunicipioCiudad", data.Worker.WorkplaceCityCode),
                    new XAttribute("LugarTrabajoDireccion", data.Worker.WorkplaceAddress),
                    new XAttribute("SalarioIntegral", B(data.Worker.IntegralSalary)),
                    new XAttribute("TipoContrato", data.Worker.ContractTypeCode),
                    new XAttribute("Sueldo", M(data.Worker.Salary)),
                    new XAttribute("CodigoTrabajador", data.Worker.WorkerCode)
                ),

                new XElement("Pago",
                    new XAttribute("Forma", data.Payment.MeansCode),
                    new XAttribute("Metodo", data.Payment.MethodCode),
                    new XAttribute("Banco", data.Payment.Bank),
                    new XAttribute("TipoCuenta", data.Payment.AccountType),
                    new XAttribute("NumeroCuenta", data.Payment.AccountNumber)
                ),

                new XElement("FechasPagos", data.PaymentDates.Select(d => new XElement("FechaPago", D(d)))),

                BuildDevengados(data.Earnings),
                BuildDeducciones(data.Deductions),

                new XElement("Redondeo", M(data.Rounding)),
                new XElement("DevengadosTotal", M(data.EarningsTotal)),
                new XElement("DeduccionesTotal", M(data.DeductionsTotal)),
                new XElement("ComprobanteTotal", M(data.PayableTotal))
            );

            return root;
        }

        private static XElement BuildDevengados(PayrollEarningsData e)
        {
            return new XElement("Devengados",
                new XElement("Basico", new XAttribute("DiasTrabajados", e.WorkedDays), new XAttribute("SueldoTrabajado", M(e.BasicSalaryPaid))),
                new XElement("Transporte",
                    new XAttribute("AuxilioTransporte", M(e.TransportAllowance)),
                    new XAttribute("ViaticoManuAlojS", M(e.PerDiemTaxable)),
                    new XAttribute("ViaticoManuAlojNS", M(e.PerDiemNonTaxable))),

                new XElement("HEDs", e.ExtraDiurnas.Select(h => new XElement("HED", TimeRangeAttrs(h)))),
                new XElement("HENs", e.ExtraNocturnas.Select(h => new XElement("HEN", TimeRangeAttrs(h)))),
                new XElement("HRNs", e.RecargoNocturno.Select(h => new XElement("HRN", TimeRangeAttrs(h)))),
                new XElement("HEDDFs", e.ExtraDiurnaFestiva.Select(h => new XElement("HEDDF", TimeRangeAttrs(h)))),
                new XElement("HRDDFs", e.RecargoDiurnoFestivo.Select(h => new XElement("HRDDF", TimeRangeAttrs(h)))),
                new XElement("HENDFs", e.ExtraNocturnaFestiva.Select(h => new XElement("HENDF", TimeRangeAttrs(h)))),
                new XElement("HRNDFs", e.RecargoNocturnoFestivo.Select(h => new XElement("HRNDF", TimeRangeAttrs(h)))),

                new XElement("Vacaciones",
                    e.VacacionesComunes.Select(v => new XElement("VacacionesComunes",
                        new XAttribute("FechaInicio", D(v.Start)), new XAttribute("FechaFin", D(v.End)),
                        new XAttribute("Cantidad", v.Quantity), new XAttribute("Pago", M(v.Amount)))),
                    new XElement("VacacionesCompensadas",
                        new XAttribute("Cantidad", e.VacacionesCompensadasCantidad),
                        new XAttribute("Pago", M(e.VacacionesCompensadasPago)))
                ),

                new XElement("Primas", new XAttribute("Cantidad", e.PrimasCantidad), new XAttribute("Pago", M(e.PrimasTaxable)), new XAttribute("PagoNS", M(e.PrimasNonTaxable))),
                new XElement("Cesantias", new XAttribute("Pago", M(e.CesantiasPago)), new XAttribute("Porcentaje", M(e.CesantiasPorcentaje)), new XAttribute("PagoIntereses", M(e.CesantiasPagoIntereses))),

                new XElement("Incapacidades", e.Incapacidades.Select(i => new XElement("Incapacidad",
                    new XAttribute("FechaInicio", D(i.Start)), new XAttribute("FechaFin", D(i.End)),
                    new XAttribute("Cantidad", i.Quantity), new XAttribute("Tipo", i.TypeCode), new XAttribute("Pago", M(i.Amount))))),

                new XElement("Licencias",
                    e.LicenciaMaternidadPaternidad.Select(l => new XElement("LicenciaMP",
                        new XAttribute("FechaInicio", D(l.Start)), new XAttribute("FechaFin", D(l.End)),
                        new XAttribute("Cantidad", l.Quantity), new XAttribute("Pago", M(l.Amount)))),
                    e.LicenciaRemunerada.Select(l => new XElement("LicenciaR",
                        new XAttribute("FechaInicio", D(l.Start)), new XAttribute("FechaFin", D(l.End)),
                        new XAttribute("Cantidad", l.Quantity), new XAttribute("Pago", M(l.Amount)))),
                    e.LicenciaNoRemunerada.Select(l => new XElement("LicenciaNR",
                        new XAttribute("FechaInicio", D(l.Start)), new XAttribute("FechaFin", D(l.End)),
                        new XAttribute("Cantidad", l.Quantity)))
                ),

                new XElement("Bonificaciones", new XElement("Bonificacion", new XAttribute("BonificacionS", M(e.BonificacionesTaxable)), new XAttribute("BonificacionNS", M(e.BonificacionesNonTaxable)))),
                new XElement("Auxilios", new XElement("Auxilio", new XAttribute("AuxilioS", M(e.AuxiliosTaxable)), new XAttribute("AuxilioNS", M(e.AuxiliosNonTaxable)))),

                new XElement("OtrosConceptos", e.OtrosConceptos.Select(o => new XElement("OtroConcepto",
                    new XAttribute("DescripcionConcepto", o.Description), new XAttribute("ConceptoS", M(o.TaxableAmount)), new XAttribute("ConceptoNS", M(o.NonTaxableAmount))))),

                new XElement("Compensaciones", new XElement("Compensacion", new XAttribute("CompensacionO", M(e.CompensacionOrdinaria)), new XAttribute("CompensacionE", M(e.CompensacionExtraordinaria)))),

                new XElement("BonoEPCTVs", new XElement("BonoEPCTV",
                    new XAttribute("PagoS", M(e.BonoEPCTVTaxable)), new XAttribute("PagoNS", M(e.BonoEPCTVNonTaxable)),
                    new XAttribute("PagoAlimentacionS", M(e.BonoEPCTVAlimentacionTaxable)), new XAttribute("PagoAlimentacionNS", M(e.BonoEPCTVAlimentacionNonTaxable)))),

                new XElement("Comisiones", new XElement("Comision", M(e.Comisiones))),
                new XElement("PagosTerceros", new XElement("PagoTercero", M(e.PagosTerceros))),
                new XElement("Anticipos", new XElement("Anticipo", M(e.Anticipos))),
                new XElement("Dotacion", M(e.Dotacion)),
                new XElement("ApoyoSost", M(e.ApoyoSostenimiento)),
                new XElement("Teletrabajo", M(e.Teletrabajo)),
                new XElement("BonifRetiro", M(e.BonificacionRetiro)),
                new XElement("Indemnizacion", M(e.Indemnizacion)),
                new XElement("Reintegro", M(e.Reintegro))
            );
        }

        private static XElement BuildDeducciones(PayrollDeductionsData d)
        {
            return new XElement("Deducciones",
                new XElement("Salud", new XAttribute("Porcentaje", M(d.SaludPorcentaje)), new XAttribute("Deduccion", M(d.SaludDeduccion))),
                new XElement("FondoPension", new XAttribute("Porcentaje", M(d.FondoPensionPorcentaje)), new XAttribute("Deduccion", M(d.FondoPensionDeduccion))),
                new XElement("FondoSP",
                    new XAttribute("Porcentaje", M(d.FondoSPPorcentaje)), new XAttribute("DeduccionSP", M(d.FondoSPDeduccion)),
                    new XAttribute("PorcentajeSub", M(d.FondoSubsistenciaPorcentaje)), new XAttribute("DeduccionSub", M(d.FondoSubsistenciaDeduccion))),
                new XElement("Sindicatos", new XElement("Sindicato", new XAttribute("Porcentaje", "0.00"), new XAttribute("Deduccion", M(d.Sindicatos)))),
                new XElement("Sanciones", new XElement("Sancion", new XAttribute("SancionPublic", M(d.SancionesPublicas)), new XAttribute("SancionPriv", M(d.SancionesPrivadas)))),
                new XElement("Libranzas", new XElement("Libranza", new XAttribute("Descripcion", ""), new XAttribute("Deduccion", M(d.Libranzas)))),
                new XElement("PagosTerceros", new XElement("PagoTercero", M(d.PagosTerceros))),
                new XElement("Anticipos", new XElement("Anticipo", M(d.Anticipos))),
                new XElement("OtrasDeducciones", new XElement("OtraDeduccion", M(d.OtrasDeducciones))),
                new XElement("PensionVoluntaria", M(d.PensionVoluntaria)),
                new XElement("RetencionFuente", M(d.RetencionFuente)),
                new XElement("AFC", M(d.AFC)),
                new XElement("Cooperativa", M(d.Cooperativa)),
                new XElement("EmbargoFiscal", M(d.EmbargoFiscal)),
                new XElement("PlanComplementarios", M(d.PlanComplementarios)),
                new XElement("Educacion", M(d.Educacion)),
                new XElement("Reintegro", M(d.Reintegro)),
                new XElement("Deuda", M(d.Deuda))
            );
        }

        private static object[] TimeRangeAttrs(PayrollTimeRange h) => new object[]
        {
            new XAttribute("HoraInicio", h.Start.ToString("yyyy-MM-ddTHH:mm:ss")),
            new XAttribute("HoraFin", h.End.ToString("yyyy-MM-ddTHH:mm:ss")),
            new XAttribute("Cantidad", h.Quantity),
            new XAttribute("Porcentaje", M(h.Percentage)),
            new XAttribute("Pago", M(h.Amount))
        };

        internal static string D(System.DateTime date) => date.ToString("yyyy-MM-dd");
        internal static string B(bool value) => value ? "true" : "false";
        internal static string M(decimal value) => value.ToString("0.00", CultureInfo.InvariantCulture);

        internal static string BuildQrUrl(string environment, string cune) =>
            environment == "1"
                ? $"https://catalogo-vpfe.dian.gov.co/document/searchqr?documentkey={cune}"
                : $"https://catalogo-vpfe-hab.dian.gov.co/document/searchqr?documentkey={cune}";
    }
}
