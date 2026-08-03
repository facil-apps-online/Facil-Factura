using System;
using System.Collections.Generic;

namespace Fel.Core.Entities
{
    public class Document
    {
        public Guid Id { get; set; }
        public Guid ClientId { get; set; }
        public Client Client { get; set; } = null!;
        
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
        
        public DateTime CreatedAt { get; set; }
        
        public Guid? DocumentTypeId { get; set; }
        public DocumentType? DocumentType { get; set; }
        
        public decimal PriceCharged { get; set; } // Valor facturado por este documento
        
        public DateTime? ProcessedAt { get; set; }
        
        // Histórico de plantilla usada para garantizar inmutabilidad visual
        public Guid? UsedTemplateId { get; set; }
        public DocumentTemplate? UsedTemplate { get; set; }

        public decimal Subtotal { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal TotalAmount { get; set; }
        public string Notes { get; set; } = string.Empty;
        public string SectorExtensionData { get; set; } = "{}";

        public Guid? ReferenceDocumentId { get; set; }
        public Document? ReferenceDocument { get; set; }
        public string ReferenceConcept { get; set; } = string.Empty;

        public ICollection<DocumentItem> Items { get; set; } = new List<DocumentItem>();
    }
}
