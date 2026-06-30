using System;

namespace Fel.Core.Entities
{
    public class Document
    {
        public Guid Id { get; set; }
        public Guid ClientId { get; set; }
        public Client Client { get; set; } = null!;
        
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
    }
}
