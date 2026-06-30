using System;
using System.Collections.Generic;

namespace Fel.Core.Models
{
    // Clase Base con campos comunes a todos
    public class DocumentRequestBase
    {
        public string Prefix { get; set; } = string.Empty;
        public string DocumentNumber { get; set; } = string.Empty;
        public DateTime IssueDate { get; set; } = DateTime.UtcNow;
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
        public string BillingReferenceCufe { get; set; } = string.Empty;
        public string DiscrepancyResponseCode { get; set; } = "2"; // 2=Anulación
        public string DiscrepancyDescription { get; set; } = string.Empty;
    }

    public class DebitNoteRequest : CreditNoteRequest
    {
        // Mismos campos base que la nota crédito
    }

    public class PayrollRequest : DocumentRequestBase
    {
        public WorkerData Worker { get; set; } = new WorkerData();
        public decimal BasicSalary { get; set; }
        public decimal TransportAllowance { get; set; }
        public decimal Deductions { get; set; }
    }

    public class WorkerData
    {
        public string Identification { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string ContractType { get; set; } = string.Empty;
    }

    public class PosDocumentRequest : InvoiceRequest
    {
        public string PosPointOfSaleId { get; set; } = string.Empty;
        public string HardwareId { get; set; } = string.Empty;
    }

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

    // Nuevo: Transporte (Solicitado por el usuario)
    public class TransportInvoiceRequest : InvoiceRequest
    {
        public TransportData TransportDetails { get; set; } = new TransportData();
    }

    public class TransportData
    {
        public string RadicacionRemesa { get; set; } = string.Empty; // MinTransporte RNDC
        public decimal ValorFlete { get; set; }
        public string PlacaVehiculo { get; set; } = string.Empty;
    }

    public class SupportDocumentRequest : InvoiceRequest
    {
        public string SellerIdentification { get; set; } = string.Empty;
        public string SellerName { get; set; } = string.Empty;
    }

    public class ReceptionEventRequest : DocumentRequestBase
    {
        public string EventCode { get; set; } = string.Empty; // 030 (Acuse), 032 (Recibo Bien), 033 (Aceptación)
        public string IssuerName { get; set; } = string.Empty;
        public string IssuerTaxId { get; set; } = string.Empty;
    }
}
