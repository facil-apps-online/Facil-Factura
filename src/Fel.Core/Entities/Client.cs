using System;
using System.Collections.Generic;

namespace Fel.Core.Entities
{
    public class Client
    {
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }
        public Tenant Tenant { get; set; } = null!;
        
        public string CompanyName { get; set; } = string.Empty;
        public string CommercialName { get; set; } = string.Empty;
        public string TaxId { get; set; } = string.Empty; // NIT
        public string VerificationDigit { get; set; } = string.Empty; // DV
        
        // Ubicación y Contacto
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        
        // Fiscal
        public string TaxRegime { get; set; } = string.Empty;
        public string EconomicActivity { get; set; } = string.Empty;
        
        // Georeferenciación
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }

        // --- DIAN Habilitation & Software Propio ---
        public string SoftwareId { get; set; } = string.Empty;
        public string SoftwarePin { get; set; } = string.Empty;
        public string TestSetId { get; set; } = string.Empty;
        public string DianHabilitationStatus { get; set; } = "Pending"; // Pending, InProgress, Passed, Production
        public int DianHabilitationProgress { get; set; } = 0;
        public string DianHabilitationMessage { get; set; } = string.Empty;
        
        // --- API Integration (HMAC) ---
        public string LiveApiKey { get; set; } = Guid.NewGuid().ToString("N");
        public string LiveApiSecret { get; set; } = Guid.NewGuid().ToString("N");
        public string TestApiKey { get; set; } = "test_" + Guid.NewGuid().ToString("N");
        public string TestApiSecret { get; set; } = Guid.NewGuid().ToString("N");
        
        // --- Billing ---
        public decimal PricePerDocument { get; set; } = 0m; // Default price set by Tenant for this Client
        
        public DateTime CreatedAt { get; set; }
        public bool IsActive { get; set; }

        public ICollection<Resolution> Resolutions { get; set; } = new List<Resolution>();
        public ICollection<Certificate> Certificates { get; set; } = new List<Certificate>();
        public ICollection<Document> Documents { get; set; } = new List<Document>();
        public ICollection<ClientUser> Users { get; set; } = new List<ClientUser>();
        public ICollection<Customer> Customers { get; set; } = new List<Customer>();
        public ICollection<Product> Products { get; set; } = new List<Product>();
    }
}
