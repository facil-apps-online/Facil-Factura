using System;
using System.Collections.Generic;

namespace Fel.Core.Models
{
    // Simplified structure to represent the JSON received from Tenants
    public class UblInvoiceData
    {
        public string DocumentNumber { get; set; } = string.Empty;
        public string Prefix { get; set; } = string.Empty;
        public DateTime IssueDate { get; set; }
        public DateTime IssueTime { get; set; }
        
        public string TechnicalKey { get; set; } = string.Empty;
        public string SoftwarePin { get; set; } = string.Empty;
        public string Environment { get; set; } = "2"; // 1=Prod, 2=Pruebas
        
        // Metadata DIAN
        public string DianCode { get; set; } = "01"; 
        public string OperationType { get; set; } = "10";
        public string? CustomizationId { get; set; }

        public IssuerData Issuer { get; set; } = new IssuerData();
        public CustomerData Customer { get; set; } = new CustomerData();
        
        public decimal LineExtensionAmount { get; set; } // ValFac
        public decimal TaxExclusiveAmount { get; set; } // Base Impuestos
        public decimal TaxInclusiveAmount { get; set; } // ValTol
        public decimal PayableAmount { get; set; } // A pagar

        public string Currency { get; set; } = "COP";
        public ExchangeRateData? ExchangeRate { get; set; }

        // Referencias para Notas Crédito / Débito
        public string? BillingReferenceDocumentNumber { get; set; }
        public DateTime? BillingReferenceDate { get; set; }
        public string? BillingReferenceCufe { get; set; }
        public string? DiscrepancyResponseCode { get; set; }
        public string? DiscrepancyDescription { get; set; }

        public List<PaymentMeansData> PaymentMeans { get; set; } = new List<PaymentMeansData>();
        public List<AllowanceChargeData> AllowanceCharges { get; set; } = new List<AllowanceChargeData>();
        public List<TaxSubtotal> Taxes { get; set; } = new List<TaxSubtotal>();
        public List<InvoiceLine> Lines { get; set; } = new List<InvoiceLine>();
    }

    public class IssuerData
    {
        public string TaxId { get; set; } = string.Empty;
        public string IdentificationCode { get; set; } = "31"; // NIT
        public string Name { get; set; } = string.Empty;
        public List<string> TaxLevelCodes { get; set; } = new List<string> { "O-47" };
        public string TaxSchemeId { get; set; } = "01";
        public string DepartmentCode { get; set; } = "11";
        public string CityCode { get; set; } = "11001";
        public string PostalZone { get; set; } = "110011";
        public string Address { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
    }

    public class CustomerData
    {
        public string TaxId { get; set; } = string.Empty;
        public string IdentificationCode { get; set; } = "13"; // CC por defecto
        public string TaxSchemeId { get; set; } = "ZY"; // No responsable por defecto
        public List<string> TaxLevelCodes { get; set; } = new List<string> { "R-99-PN" };
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string DepartmentCode { get; set; } = string.Empty;
        public string CityCode { get; set; } = string.Empty;
        public string PostalZone { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
    }

    public class TaxSubtotal
    {
        public string TaxId { get; set; } = "01";
        public decimal TaxAmount { get; set; }
        public decimal TaxableAmount { get; set; }
        public decimal Percent { get; set; }
    }

    public class PaymentMeansData
    {
        public string Id { get; set; } = "1"; // 1=Contado, 2=Crédito
        public string PaymentMeansCode { get; set; } = "10"; // 10=Efectivo
        public DateTime? PaymentDueDate { get; set; }
    }

    public class AllowanceChargeData
    {
        public bool ChargeIndicator { get; set; } // false=Descuento, true=Recargo
        public string ReasonCode { get; set; } = "00";
        public string Reason { get; set; } = string.Empty;
        public decimal Percentage { get; set; }
        public decimal BaseAmount { get; set; }
        public decimal Amount { get; set; }
    }

    public class ExchangeRateData
    {
        public decimal CalculationRate { get; set; }
        public string SourceCurrencyCode { get; set; } = "USD";
        public string TargetCurrencyCode { get; set; } = "COP";
        public DateTime Date { get; set; }
    }

    public class InvoiceLine
    {
        public string ItemCode { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal LineExtensionAmount { get; set; }
        public List<TaxSubtotal> Taxes { get; set; } = new List<TaxSubtotal>();
        public List<AllowanceChargeData> AllowanceCharges { get; set; } = new List<AllowanceChargeData>();
    }
}
