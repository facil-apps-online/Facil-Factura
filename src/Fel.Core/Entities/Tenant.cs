using System;
using System.Collections.Generic;

namespace Fel.Core.Entities
{
    public class Tenant
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string CommercialName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        
        // --- Fiscal & Contact Info ---
        public string TaxId { get; set; } = string.Empty;
        public string VerificationDigit { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string TaxRegime { get; set; } = string.Empty;
        public string EconomicActivity { get; set; } = string.Empty;
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        
        // --- Branding (White Label) ---
        public string Slug { get; set; } = string.Empty; // e.g. "mi-empresa"
        public string LogoLightUrl { get; set; } = string.Empty;
        public string LogoDarkUrl { get; set; } = string.Empty;
        public string PrimaryColorLight { get; set; } = "#0f172a"; // Tailwind Slate-900 default
        public string PrimaryColorDark { get; set; } = "#f8fafc";  // Tailwind Slate-50 default
        
        public DateTime CreatedAt { get; set; }
        public bool IsActive { get; set; }

        public ICollection<Client> Clients { get; set; } = new List<Client>();
        public ICollection<TenantPricing> Pricings { get; set; } = new List<TenantPricing>();
        public ICollection<TenantBilling> Billings { get; set; } = new List<TenantBilling>();
        public ICollection<TenantUser> Users { get; set; } = new List<TenantUser>();
    }
}
