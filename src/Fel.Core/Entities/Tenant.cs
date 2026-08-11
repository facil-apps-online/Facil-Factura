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
        public string? CoreTenantId { get; set; } // Id del tenant comercial en la base Core (Supabase)
        public string LogoLightUrl { get; set; } = string.Empty;
        public string LogoDarkUrl { get; set; } = string.Empty;
        public string PrimaryColorLight { get; set; } = "#0f172a"; // Tailwind Slate-900 default
        public string PrimaryColorDark { get; set; } = "#f8fafc";  // Tailwind Slate-50 default

        // --- Comercial / Contacto (espejo de tabla tenants de Core) ---
        public string? LegalName { get; set; }              // Razón social jurídica
        public string? ContactPerson { get; set; }          // Persona de contacto
        public string? ContactEmail { get; set; }
        public string? ContactPhone { get; set; }
        public string? WhatsAppPhone { get; set; }
        public string? EinvoicingEmail { get; set; }        // Correo de facturación electrónica
        public string? CommercialEmail { get; set; }
        public string? Website { get; set; }

        // --- Dirección física ---
        public string? PhysicalAddressLine1 { get; set; }
        public string? PhysicalAddressLine2 { get; set; }
        public string? PhysicalCity { get; set; }
        public string? PhysicalState { get; set; }
        public string? PhysicalPostalCode { get; set; }

        // --- Dirección de facturación ---
        public string? BillingAddress { get; set; }

        // --- Regionalización (espejo de Core) ---
        public string DefaultLanguageCode { get; set; } = "es-CO";
        public string DefaultTimezone { get; set; } = "America/Bogota";
        public string? DefaultCurrencyId { get; set; }       // Guid de tabla currencies en Core
        
        public DateTime CreatedAt { get; set; }
        public bool IsActive { get; set; }

        public ICollection<Client> Clients { get; set; } = new List<Client>();
        public ICollection<TenantPricing> Pricings { get; set; } = new List<TenantPricing>();
        public ICollection<TenantBilling> Billings { get; set; } = new List<TenantBilling>();
        public ICollection<TenantUser> Users { get; set; } = new List<TenantUser>();
    }
}
