using System;
using System.Collections.Generic;

namespace Fel.Core.Models
{
    // Modelo fiel al Anexo Técnico Documento Soporte de Pago de Nómina Electrónica v1.0 (DIAN,
    // Resolución 000013 de 2021) — ver docs/dian-nomina/ en la raíz del repo. Nómina NO es UBL
    // Invoice-family (su firma sí usa UBL 2.1, pero el resto del documento tiene esquema propio:
    // dian:gov:co:facturaelectronica:NominaIndividual), así que vive en su propio modelo en vez de
    // forzarlo dentro de UblInvoiceData.
    public class UblPayrollData
    {
        public string DocumentNumber { get; set; } = string.Empty; // Consecutivo
        public string Prefix { get; set; } = string.Empty;
        public DateTime IssueDate { get; set; } // FechaGen
        public DateTime IssueTime { get; set; } // HoraGen

        public string SoftwareId { get; set; } = string.Empty;
        public string SoftwarePin { get; set; } = string.Empty;
        public string Environment { get; set; } = "2"; // 1=Producción, 2=Habilitación
        public string DianCode { get; set; } = "102"; // 102=NominaIndividual, 103=NominaIndividualDeAjuste

        public PayrollPartyData Employer { get; set; } = new PayrollPartyData();
        public WorkerPayrollData Worker { get; set; } = new WorkerPayrollData();
        public PayrollPeriodData Period { get; set; } = new PayrollPeriodData();
        public PayrollPaymentData Payment { get; set; } = new PayrollPaymentData();
        public List<DateTime> PaymentDates { get; set; } = new List<DateTime>();

        public PayrollEarningsData Earnings { get; set; } = new PayrollEarningsData();
        public PayrollDeductionsData Deductions { get; set; } = new PayrollDeductionsData();

        public decimal Rounding { get; set; }
        public decimal EarningsTotal { get; set; }
        public decimal DeductionsTotal { get; set; }
        public decimal PayableTotal { get; set; }

        public string Notes { get; set; } = string.Empty;
    }

    // Empleador (y también ProveedorXML, que en el modelo autogestionado de este sistema siempre es
    // el mismo emisor — ver comentario en BaseUblStrategy.BuildExtensions).
    public class PayrollPartyData
    {
        public string TaxId { get; set; } = string.Empty; // NIT, sin DV
        public string VerificationDigit { get; set; } = string.Empty;
        public string CompanyName { get; set; } = string.Empty; // RazonSocial (persona jurídica)
        public string CountryCode { get; set; } = "CO";
        public string DepartmentCode { get; set; } = string.Empty;
        public string CityCode { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
    }

    public class WorkerPayrollData
    {
        public string WorkerTypeCode { get; set; } = "01"; // TipoTrabajador
        public string WorkerSubTypeCode { get; set; } = "00"; // SubTipoTrabajador
        public bool HighPensionRisk { get; set; } = false; // AltoRiesgoPension
        public string IdentificationTypeCode { get; set; } = "13"; // CC por defecto
        public string IdentificationNumber { get; set; } = string.Empty;
        public string FirstSurname { get; set; } = string.Empty;
        public string SecondSurname { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string OtherNames { get; set; } = string.Empty;
        public string WorkplaceCountryCode { get; set; } = "CO";
        public string WorkplaceDepartmentCode { get; set; } = string.Empty;
        public string WorkplaceCityCode { get; set; } = string.Empty;
        public string WorkplaceAddress { get; set; } = string.Empty;
        public bool IntegralSalary { get; set; } = false;
        public string ContractTypeCode { get; set; } = "1";
        public decimal Salary { get; set; }
        public string WorkerCode { get; set; } = string.Empty; // CodigoTrabajador (interno del emisor)
    }

    public class PayrollPeriodData
    {
        public DateTime HireDate { get; set; } // FechaIngreso
        public DateTime? TerminationDate { get; set; } // FechaRetiro
        public DateTime SettlementStart { get; set; } // FechaLiquidacionInicio
        public DateTime SettlementEnd { get; set; } // FechaLiquidacionFin
        public int WorkedTime { get; set; } // TiempoLaborado (días)
    }

    public class PayrollPaymentData
    {
        public string MeansCode { get; set; } = "1"; // Forma: 1=Contado
        public string MethodCode { get; set; } = "10"; // Metodo: 10=Efectivo
        public string Bank { get; set; } = string.Empty;
        public string AccountType { get; set; } = string.Empty;
        public string AccountNumber { get; set; } = string.Empty;
    }

    public class PayrollTimeRange
    {
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
        public decimal Quantity { get; set; }
        public decimal Percentage { get; set; }
        public decimal Amount { get; set; }
    }

    public class PayrollDateRangeAmount
    {
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
        public decimal Quantity { get; set; }
        public decimal Amount { get; set; }
    }

    public class PayrollIncapacity
    {
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
        public int Quantity { get; set; }
        public string TypeCode { get; set; } = "1"; // 1=Común, 2=Laboral
        public decimal Amount { get; set; }
    }

    public class PayrollOtherConcept
    {
        public string Description { get; set; } = string.Empty;
        public decimal TaxableAmount { get; set; } // ConceptoS
        public decimal NonTaxableAmount { get; set; } // ConceptoNS
    }

    // Devengados — catálogo completo del Anexo Técnico 5.x. Todo en 0 por defecto; solo se
    // completa lo que aplique a cada trabajador.
    public class PayrollEarningsData
    {
        public int WorkedDays { get; set; }
        public decimal BasicSalaryPaid { get; set; } // SueldoTrabajado

        public decimal TransportAllowance { get; set; }
        public decimal PerDiemTaxable { get; set; } // ViaticoManuAlojS
        public decimal PerDiemNonTaxable { get; set; } // ViaticoManuAlojNS

        public List<PayrollTimeRange> ExtraDiurnas { get; set; } = new(); // HEDs
        public List<PayrollTimeRange> ExtraNocturnas { get; set; } = new(); // HENs
        public List<PayrollTimeRange> RecargoNocturno { get; set; } = new(); // HRNs
        public List<PayrollTimeRange> ExtraDiurnaFestiva { get; set; } = new(); // HEDDFs
        public List<PayrollTimeRange> RecargoDiurnoFestivo { get; set; } = new(); // HRDDFs
        public List<PayrollTimeRange> ExtraNocturnaFestiva { get; set; } = new(); // HENDFs
        public List<PayrollTimeRange> RecargoNocturnoFestivo { get; set; } = new(); // HRNDFs

        public List<PayrollDateRangeAmount> VacacionesComunes { get; set; } = new();
        public int VacacionesCompensadasCantidad { get; set; }
        public decimal VacacionesCompensadasPago { get; set; }

        public int PrimasCantidad { get; set; }
        public decimal PrimasTaxable { get; set; }
        public decimal PrimasNonTaxable { get; set; }

        public decimal CesantiasPago { get; set; }
        public decimal CesantiasPorcentaje { get; set; }
        public decimal CesantiasPagoIntereses { get; set; }

        public List<PayrollIncapacity> Incapacidades { get; set; } = new();

        public List<PayrollDateRangeAmount> LicenciaMaternidadPaternidad { get; set; } = new();
        public List<PayrollDateRangeAmount> LicenciaRemunerada { get; set; } = new();
        public List<(DateTime Start, DateTime End, int Quantity)> LicenciaNoRemunerada { get; set; } = new();

        public decimal BonificacionesTaxable { get; set; }
        public decimal BonificacionesNonTaxable { get; set; }
        public decimal AuxiliosTaxable { get; set; }
        public decimal AuxiliosNonTaxable { get; set; }

        public List<PayrollOtherConcept> OtrosConceptos { get; set; } = new();

        public decimal CompensacionOrdinaria { get; set; }
        public decimal CompensacionExtraordinaria { get; set; }

        public decimal BonoEPCTVTaxable { get; set; }
        public decimal BonoEPCTVNonTaxable { get; set; }
        public decimal BonoEPCTVAlimentacionTaxable { get; set; }
        public decimal BonoEPCTVAlimentacionNonTaxable { get; set; }

        public decimal Comisiones { get; set; }
        public decimal PagosTerceros { get; set; }
        public decimal Anticipos { get; set; }
        public decimal Dotacion { get; set; }
        public decimal ApoyoSostenimiento { get; set; }
        public decimal Teletrabajo { get; set; }
        public decimal BonificacionRetiro { get; set; }
        public decimal Indemnizacion { get; set; }
        public decimal Reintegro { get; set; }
    }

    public class PayrollDeductionsData
    {
        public decimal SaludPorcentaje { get; set; }
        public decimal SaludDeduccion { get; set; }

        public decimal FondoPensionPorcentaje { get; set; }
        public decimal FondoPensionDeduccion { get; set; }

        public decimal FondoSPPorcentaje { get; set; }
        public decimal FondoSPDeduccion { get; set; }
        public decimal FondoSubsistenciaPorcentaje { get; set; }
        public decimal FondoSubsistenciaDeduccion { get; set; }

        public decimal Sindicatos { get; set; }
        public decimal SancionesPublicas { get; set; }
        public decimal SancionesPrivadas { get; set; }
        public decimal Libranzas { get; set; }
        public decimal PagosTerceros { get; set; }
        public decimal Anticipos { get; set; }
        public decimal OtrasDeducciones { get; set; }
        public decimal PensionVoluntaria { get; set; }
        public decimal RetencionFuente { get; set; }
        public decimal AFC { get; set; }
        public decimal Cooperativa { get; set; }
        public decimal EmbargoFiscal { get; set; }
        public decimal PlanComplementarios { get; set; }
        public decimal Educacion { get; set; }
        public decimal Reintegro { get; set; }
        public decimal Deuda { get; set; }
    }

    // Anulación (NominaIndividualDeAjuste, rama Eliminar) — no lleva devengados/deducciones, solo
    // referencia el documento que se está anulando.
    public class UblPayrollVoidData
    {
        public string DocumentNumber { get; set; } = string.Empty;
        public string Prefix { get; set; } = string.Empty;
        public DateTime IssueDate { get; set; }
        public DateTime IssueTime { get; set; }

        public string SoftwareId { get; set; } = string.Empty;
        public string SoftwarePin { get; set; } = string.Empty;
        public string Environment { get; set; } = "2";

        public PayrollPartyData Employer { get; set; } = new PayrollPartyData();

        // Documento que se está anulando.
        public string PredecessorNumber { get; set; } = string.Empty;
        public string PredecessorCune { get; set; } = string.Empty;
        public DateTime PredecessorIssueDate { get; set; }

        public string Notes { get; set; } = string.Empty;
    }
}
