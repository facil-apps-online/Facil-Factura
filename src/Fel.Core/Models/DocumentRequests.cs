using System;
using System.Collections.Generic;

namespace Fel.Core.Models
{
    // Clase Base con campos comunes a todos
    public class DocumentRequestBase
    {
        public string Prefix { get; set; } = string.Empty;
        public string DocumentNumber { get; set; } = string.Empty;
        public DateTime IssueDate { get; set; } = ColombiaTime.Now;
        public string Currency { get; set; } = "COP";
        public decimal TotalAmount { get; set; }
        public ExchangeRateData? ExchangeRate { get; set; }
    }

    public class InvoiceRequest : DocumentRequestBase
    {
        public IssuerData Issuer { get; set; } = new IssuerData();
        public CustomerData Customer { get; set; } = new CustomerData();
        
        public List<PaymentMeansData> PaymentMeans { get; set; } = new List<PaymentMeansData>();
        public List<AllowanceChargeData> AllowanceCharges { get; set; } = new List<AllowanceChargeData>();
        public List<TaxSubtotal> Taxes { get; set; } = new List<TaxSubtotal>();
        public List<InvoiceLine> Lines { get; set; } = new List<InvoiceLine>();
    }

    public class CreditNoteRequest : InvoiceRequest
    {
        // Nota referenciada: los tres deben venir juntos (la DIAN exige CUFE + número de la
        // factura original dentro de InvoiceDocumentReference). Si BillingReferenceCufe viene
        // vacío, la nota se arma como no referenciada (ej. ajustes que no corresponden a una
        // factura electrónica puntual).
        public string BillingReferenceCufe { get; set; } = string.Empty;
        public string BillingReferenceDocumentNumber { get; set; } = string.Empty;
        public DateTime? BillingReferenceDate { get; set; }

        public string DiscrepancyResponseCode { get; set; } = "2"; // 2=Anulación
        public string DiscrepancyDescription { get; set; } = string.Empty;
    }

    public class DebitNoteRequest : CreditNoteRequest
    {
        // Mismos campos base que la nota crédito
    }

    // El emisor (Empleador) y los datos de habilitación (SoftwareId/Pin, Ambiente) siempre salen
    // del Client autenticado, igual que en InvoiceRequest — no se declaran aquí.
    public class PayrollRequest : DocumentRequestBase
    {
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

    // Anulación (TipoNota=2 "Eliminar") de un Documento Soporte de Pago de Nómina ya transmitido.
    public class PayrollVoidRequest
    {
        public string Prefix { get; set; } = string.Empty;
        public string DocumentNumber { get; set; } = string.Empty;
        public DateTime IssueDate { get; set; } = ColombiaTime.Now;

        public string PredecessorNumber { get; set; } = string.Empty;
        public string PredecessorCune { get; set; } = string.Empty;
        public DateTime PredecessorIssueDate { get; set; }

        public string Notes { get; set; } = string.Empty;
    }

    public class PosDocumentRequest : InvoiceRequest
    {
        public string PosPointOfSaleId { get; set; } = string.Empty;
        public string HardwareId { get; set; } = string.Empty;

        // false = CustomizationID 601 (facturación normal), true = 602 (facturación en sitio).
        public bool OnSite { get; set; } = false;
    }

    // Los demás subtipos de Documento Equivalente Electrónico comparten la misma estructura base
    // (emisor=Client, adquirente/líneas/impuestos=lo que declare el caller) — solo cambia el
    // InvoiceTypeCode/ProfileID, que cada controlador fija internamente. Son clases vacías (como
    // DebitNoteRequest : CreditNoteRequest) solo para que cada endpoint tenga su propio tipo en el
    // Swagger, no porque necesiten campos propios verificados todavía.
    public class CinemaDocumentRequest : InvoiceRequest { }
    public class PublicShowDocumentRequest : InvoiceRequest { }
    public class LocalizedGamesDocumentRequest : InvoiceRequest { }
    public class LandPassengerTransportDocumentRequest : InvoiceRequest { }
    public class TollDocumentRequest : InvoiceRequest { }
    public class FinancialStatementDocumentRequest : InvoiceRequest { }
    public class AirTransportDocumentRequest : InvoiceRequest { }
    public class StockExchangeDocumentRequest : InvoiceRequest { }
    public class PublicUtilityDocumentRequest : InvoiceRequest { }

    public class HealthInvoiceRequest : InvoiceRequest
    {
        public HealthRipsData HealthData { get; set; } = new HealthRipsData();
    }

    public class HealthRipsData
    {
        public string ProviderCode { get; set; } = string.Empty; // Código de habilitación IPS
        public string EpsCode { get; set; } = string.Empty;
        public List<ConsultationRips> Consultations { get; set; } = new List<ConsultationRips>();
    }

    public class ConsultationRips
    {
        public string PatientId { get; set; } = string.Empty;
        public string DiagnosisCode { get; set; } = string.Empty;
        public string ConsultationPurpose { get; set; } = string.Empty;
    }

    // Factura Electrónica de Transporte de Carga: NO es un tipo de documento aparte ante la DIAN —
    // es una factura normal (mismo InvoiceTypeCode, mismo CUFE, mismo webservice) con
    // OperationType=12 y, cuando aplica, cada línea marcada como remesa RNDC lleva sus propios
    // datos (ver los campos Rndc* ya agregados a InvoiceLine — no hay nada propio que agregar acá).
    public class TransportInvoiceRequest : InvoiceRequest { }

    // A diferencia de InvoiceRequest, acá el Client autenticado es el ADQUIRENTE (ABS) — quien
    // reporta la compra — no el emisor. El vendedor no obligado a facturar (SNO) no es nuestro
    // Client, así que sus datos vienen en el request. No hereda InvoiceRequest porque ese Issuer
    // implícito (siempre = Client) no aplica aquí.
    public class SupportDocumentRequest : DocumentRequestBase
    {
        public IssuerData Seller { get; set; } = new IssuerData(); // SNO: Sujeto No Obligado
        public bool SellerIsNonResident { get; set; } = false; // CustomizationID 10=Residente, 11=No residente

        public List<PaymentMeansData> PaymentMeans { get; set; } = new List<PaymentMeansData>();
        public List<AllowanceChargeData> AllowanceCharges { get; set; } = new List<AllowanceChargeData>();
        public List<TaxSubtotal> Taxes { get; set; } = new List<TaxSubtotal>();
        public List<InvoiceLine> Lines { get; set; } = new List<InvoiceLine>();
    }

    // Nota de Ajuste al Documento Soporte (DianCode 95) — referencia el CUDS del Documento Soporte
    // original. A diferencia de la emisión, esta sí va en <CreditNote>.
    public class SupportDocumentAdjustmentRequest : SupportDocumentRequest
    {
        public string BillingReferenceCuds { get; set; } = string.Empty;
        public string BillingReferenceDocumentNumber { get; set; } = string.Empty;
        public DateTime? BillingReferenceDate { get; set; }
        public string DiscrepancyResponseCode { get; set; } = "2";
        public string DiscrepancyDescription { get; set; } = string.Empty;
    }

    public class ReceptionEventRequest : DocumentRequestBase
    {
        public string EventCode { get; set; } = string.Empty; // 030 (Acuse), 032 (Recibo Bien), 033 (Aceptación)
        public string IssuerName { get; set; } = string.Empty;
        public string IssuerTaxId { get; set; } = string.Empty;
    }
}
