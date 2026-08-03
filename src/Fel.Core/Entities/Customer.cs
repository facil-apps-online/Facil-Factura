using System;

namespace Fel.Core.Entities
{
    public class Customer
    {
        public Guid Id { get; set; }
        public Guid ClientId { get; set; }
        public Client Client { get; set; } = null!;

        public string Name { get; set; } = string.Empty; // Razón Social o Nombres y Apellidos
        public string IdentificationType { get; set; } = string.Empty; // NIT, CC, CE, etc. (Ej: "31", "13")
        public string IdentificationNumber { get; set; } = string.Empty;
        public string VerificationDigit { get; set; } = string.Empty;

        // Contacto
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string CityCode { get; set; } = string.Empty; // Código DANE del municipio
        public string CityName { get; set; } = string.Empty;
        
        // Fiscal
        public string TaxRegime { get; set; } = string.Empty; // Régimen tributario (Ej: "48" o "49")
        public string FiscalResponsibilities { get; set; } = string.Empty; // Responsabilidades rut (Ej: "O-15", "O-47")

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
