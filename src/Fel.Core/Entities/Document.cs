using System;
using System.Collections.Generic;

namespace Fel.Core.Entities
{
    public class Document
    {
        public Guid Id { get; set; }
        public Guid ClientId { get; set; }
        public Client? Client { get; set; }
        
        public Guid? CustomerId { get; set; }
        public Customer? Customer { get; set; }
        
        public string TrackingId { get; set; } = string.Empty; // Transaction ID for Webhook
        public string TypeCode { get; set; } = string.Empty;
        public string Number { get; set; } = string.Empty;
        public string? Cufe { get; set; }
        
        public string Status { get; set; } = "PENDING"; // PENDING, PROCESSING, APPROVED, REJECTED
        public string? DianResponseCode { get; set; }
        public string? DianResponseMessage { get; set; }
        
        public string? XmlUrl { get; set; }
        public string? PdfUrl { get; set; }

        // ID interno que Dataico le asigna a este documento al crearlo (campo "uuid" en su
        // respuesta) — una Nota Crédito/Débito que lo referencie necesita este valor, no nuestro
        // Id ni el CUFE.
        public string? DataicoDocumentId { get; set; }
        
        public DateTime CreatedAt { get; set; }

        // Fecha de factura (emisión) editable por el cliente al crear/editar el borrador; distinta
        // de CreatedAt (momento en que se guardó el borrador en el sistema). Es lo que se envía a
        // Dataico como issue_date, y junto con PaymentTermDays determina la fecha de vencimiento.
        public DateTime IssueDate { get; set; }

        public Guid? DocumentTypeId { get; set; }
        public DocumentType? DocumentType { get; set; }
        
        public decimal PriceCharged { get; set; } // Valor facturado por este documento

        public DateTime? ProcessedAt { get; set; }

        // Histórico de plantilla usada para garantizar inmutabilidad visual
        public Guid? UsedTemplateId { get; set; }
        public DocumentTemplate? UsedTemplate { get; set; }

        // Integrador que tramitó este documento, copiado de Client.IntegratorId al emitir con
        // éxito — no se lee del Client en el momento de facturar porque Client.IntegratorId puede
        // cambiar después (el Client se mueve de proveedor) y la facturación de este documento
        // debe quedar fija al integrador que realmente lo procesó.
        public Guid? IntegratorId { get; set; }
        public Integrator? Integrator { get; set; }

        public decimal Subtotal { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal TotalAmount { get; set; }
        public string Notes { get; set; } = string.Empty;
        public string SectorExtensionData { get; set; } = "{}";

        // Resolución elegida al crear el borrador — un Client puede tener varias resoluciones
        // activas simultáneamente (una por prefijo/tipo de documento), así que no se puede
        // adivinar cuál usar al emitir; se elige explícitamente al crear/editar el documento.
        public Guid? ResolutionId { get; set; }
        public Resolution? Resolution { get; set; }

        // Guardados en el borrador para no tener que volver a pedirlos al confirmar/emitir.
        public string? PaymentMeans { get; set; } // Medio de pago (ej. EFECTIVO, TRANSFERENCIA)
        public string? PaymentMeansType { get; set; } // Tipo de pago (DEBITO=contado, CREDITO=crédito)
        public int? PaymentTermDays { get; set; } // Plazo de pago en días; fecha de vencimiento = IssueDate + este plazo
        public string? PurchaseOrderReference { get; set; } // Orden de compra del cliente/adquirente

        // Descuento general del documento completo (ej. "Pronto pago"), independiente del
        // descuento por ítem — se envía a Dataico como "charges" con discount=true.
        public string? GeneralDiscountReason { get; set; }
        public decimal? GeneralDiscountAmount { get; set; }

        // Cargo general del documento completo (ej. "Flete"), simétrico al descuento general
        // — se envía a Dataico como "charges" con discount=false.
        public string? GeneralChargeReason { get; set; }
        public decimal? GeneralChargeAmount { get; set; }

        // Retenciones (ReteICA, ReteIVA, u otras) definidas una sola vez para todo el documento en
        // vez de por ítem, como sí aplica RET_FUENTE que varía por concepto — lista libre de
        // agregar/quitar, igual que las retenciones por ítem. Al emitir se prorratean entre los
        // ítems porque Dataico exige las retenciones por ítem en su API (RET_IVA se prorratea sobre
        // el IVA generado de cada línea; el resto sobre la base gravable de cada línea).
        public ICollection<DocumentGeneralRetention> GeneralRetentions { get; set; } = new List<DocumentGeneralRetention>();

        public Guid? ReferenceDocumentId { get; set; }
        public Document? ReferenceDocument { get; set; }
        public string ReferenceConcept { get; set; } = string.Empty;

        public ICollection<DocumentItem> Items { get; set; } = new List<DocumentItem>();
    }
}
