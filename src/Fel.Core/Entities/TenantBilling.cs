using System;

namespace Fel.Core.Entities
{
    public class TenantBilling
    {
        public Guid Id { get; set; }
        
        public Guid TenantId { get; set; }
        public Tenant? Tenant { get; set; }

        public int Month { get; set; }
        public int Year { get; set; }
        
        public int TotalDocuments { get; set; }
        public decimal TotalAmount { get; set; }
        
        public string Currency { get; set; } = "COP";
        
        // "Pending", "Paid", "Overdue"
        public string Status { get; set; } = "Pending";
        
        public DateTime CreatedAt { get; set; }
        public DateTime? PaidAt { get; set; }
    }
}
