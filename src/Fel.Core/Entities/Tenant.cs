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

        // --- Facturación ---
        public TenantBillingMode BillingMode { get; set; } = TenantBillingMode.PerDocument;
        // Controla si el portal del cliente final (client-web) puede mostrar información de
        // consumo/facturación de ese cliente. El tenant decide qué tanto expone a ese nivel.
        public bool ShowUsageToClients { get; set; } = false;

        public ICollection<Client> Clients { get; set; } = new List<Client>();
        public ICollection<TenantPricing> Pricings { get; set; } = new List<TenantPricing>();
        public ICollection<TenantBilling> Billings { get; set; } = new List<TenantBilling>();
        public ICollection<TenantUser> Users { get; set; } = new List<TenantUser>();
        public TenantUserPricing? UserPricing { get; set; }
        public ICollection<PrepaidPackage> PrepaidPackages { get; set; } = new List<PrepaidPackage>();
        public ICollection<TenantPrepaidBag> PrepaidBags { get; set; } = new List<TenantPrepaidBag>();

        // --- Grupo empresarial ---
        // Un tenant puede agrupar a otros tenants (ej. R&W agrupa a DGS): cada tenant asociado
        // sigue siendo 100% independiente (sus propios Clients, branding, usuarios) — lo único
        // que cambia es que R&W puede ver el consolidado de lo que a cada uno se le ha facturado
        // (ver "Facturación de mi grupo" en el portal de Tenant). No afecta cómo se calcula
        // TenantBilling: cada tenant sigue generando su propio corte mensual igual que siempre.
        public Guid? ParentTenantId { get; set; }
        public Tenant? ParentTenant { get; set; }
        public ICollection<Tenant> ChildTenants { get; set; } = new List<Tenant>();
    }
}
