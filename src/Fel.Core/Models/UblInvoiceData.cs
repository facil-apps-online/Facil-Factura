using System;
using System.Collections.Generic;

namespace Fel.Core.Models
{
    // Simplified structure to represent the JSON received from Tenants
    public class UblInvoiceData
    {
        public string DocumentNumber { get; set; } = string.Empty;
        public string Prefix { get; set; } = string.Empty;
        // Sucursal dueña de la llave de API con la que se envió el documento (la API de integración la resuelve; el Worker la guarda).
        public Guid? BranchId { get; set; }
        public DateTime IssueDate { get; set; }
        public DateTime IssueTime { get; set; }
        
        public string TechnicalKey { get; set; } = string.Empty;
        public string SoftwareId { get; set; } = string.Empty; // Client.SoftwareId (cada cliente hace su propia habilitación)
        public string SoftwarePin { get; set; } = string.Empty;
        public string Environment { get; set; } = "2"; // 1=Prod, 2=Pruebas

        // Datos de la resolución de facturación real bajo la que se emite este documento (ver
        // Fel.Core.Entities.Resolution) — antes iban fijos en el XML con valores de 2024.
        public string ResolutionNumber { get; set; } = string.Empty;
        public DateTime ResolutionValidFrom { get; set; }
        public DateTime ResolutionValidTo { get; set; }
        public long ResolutionNumberStart { get; set; }
        public long ResolutionNumberEnd { get; set; }
        
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
        public string DepartmentName { get; set; } = string.Empty;
        public string CityCode { get; set; } = "11001";
        public string CityName { get; set; } = string.Empty;
        public string PostalZone { get; set; } = "110011";
        public string Address { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
    }

    public class CustomerData
    {
        public string TaxId { get; set; } = string.Empty;
        public string IdentificationCode { get; set; } = "13"; // CC por defecto
        // "ZZ" ("Nombre de la figura tributaria" — código genérico/otro) es el único valor de la
        // lista oficial TipoImpuesto-2.1.gc para un adquirente no responsable de IVA. "ZY" (el
        // valor anterior) no existe en esa lista — probable causa de las notificaciones FAK40/FAK41
        // ("contenido no corresponde a un contenido válido de la lista correspondiente").
        public string TaxSchemeId { get; set; } = "ZZ";
        public List<string> TaxLevelCodes { get; set; } = new List<string> { "R-99-PN" };
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string DepartmentCode { get; set; } = string.Empty;
        public string DepartmentName { get; set; } = string.Empty;
        public string CityCode { get; set; } = string.Empty;
        public string CityName { get; set; } = string.Empty;
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
        // Información adicional del artículo (cbc:Note de la línea): obligatoria en la línea de
        // Administración de un contrato AIU (ver AiuContractData.NotePrefix).
        public string? Note { get; set; }
        public decimal Quantity { get; set; }
        public string UnitCode { get; set; } = "94"; // Código DIAN de la unidad de medida (ver UnitOfMeasure)
        public decimal UnitPrice { get; set; }
        public decimal LineExtensionAmount { get; set; }
        public List<TaxSubtotal> Taxes { get; set; } = new List<TaxSubtotal>();
        public List<AllowanceChargeData> AllowanceCharges { get; set; } = new List<AllowanceChargeData>();

        // Sector Transporte de Carga (Guía de Factura Electrónica de Transporte, Mintransporte) —
        // null para cualquier documento que no sea factura de transporte. true = la línea corresponde
        // a una remesa registrada en el RNDC; false = otro servicio (escolta, montacarga, etc.).
        public bool? IsRndcRemittance { get; set; }
        public string? RndcRemittanceRadicado { get; set; } // Radicado entregado por el RNDC
        public string? RndcRemittanceConsecutive { get; set; } // Consecutivo interno de la empresa
        public decimal? RndcFreightValue { get; set; } // Valor del flete de esta remesa
        public decimal? RndcTransportedQuantity { get; set; } // Cantidad transportada
        public string? RndcTransportedUnitCode { get; set; } // Unidad (ej. "KGM")
    }
}
