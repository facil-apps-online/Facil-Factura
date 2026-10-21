using System;

namespace Fel.Core.Entities
{
    public class Customer
    {
        public Guid Id { get; set; }
        public Guid ClientId { get; set; }
        public Client? Client { get; set; }

        // Persona jurídica (IdentificationType = "31", NIT): solo se diligencia Name (razón social).
        // Persona natural (cualquier otro tipo): se diligencian los 4 campos de abajo y Name queda
        // como la concatenación de los 4 (para no romper listados/búsquedas que ya usan Name).
        public string Name { get; set; } = string.Empty; // Razón Social o Nombres y Apellidos concatenados
        public string? FirstName { get; set; } // Nombre 1
        public string? SecondName { get; set; } // Nombre 2 (opcional)
        public string? FirstLastName { get; set; } // Apellido 1
        public string? SecondLastName { get; set; } // Apellido 2 (opcional)
        public string IdentificationType { get; set; } = string.Empty; // NIT, CC, CE, etc. (Ej: "31", "13")
        public string IdentificationNumber { get; set; } = string.Empty;
        public string VerificationDigit { get; set; } = string.Empty;
        public PartyType PartyType { get; set; } = PartyType.Cliente;
        // Si este tercero es un cliente del tenant (en la empresa propietaria del tenant): el Client al que corresponde. Es el vínculo estable de la
        // sincronización de clientes del tenant, más fiable que el NIT. Nulo en cualquier otro tercero.
        public Guid? LinkedClientId { get; set; }

        // Contacto
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        // País de la dirección (alfa-2 del catálogo de países; "CO" por defecto). Fuera de Colombia no hay código DANE: la ciudad
        // (CityName) y el departamento o estado (StateName) van como texto.
        public string CountryCode { get; set; } = "CO";
        public string? StateName { get; set; }
        public string CityCode { get; set; } = string.Empty; // Código DANE del municipio
        public string CityName { get; set; } = string.Empty;
        public string? PostalCode { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }

        // Fiscal
        public string TaxRegime { get; set; } = string.Empty; // Régimen tributario (Ej: "48" o "49")
        public string FiscalResponsibilities { get; set; } = string.Empty; // Responsabilidades rut (Ej: "O-15", "O-47")

        // Requeridos por Dataico al facturar a este tercero (catálogo propio de Dataico, no DIAN)
        public string DataicoTaxLevelCode { get; set; } = string.Empty; // Ej: SIMPLIFICADO, COMUN, RESPONSABLE_DE_IVA
        public string DataicoRegimen { get; set; } = string.Empty; // Ej: SIMPLE, ORDINARIO
        // Responsabilidades fiscales (las mismas que tiene un cliente): marcas del RUT y régimen de renta (05 ordinario, 04 especial, 47 simple).
        public bool IsGranContribuyente { get; set; }
        public bool IsAgenteRetenedorIva { get; set; }
        public bool IsAutorretenedorRenta { get; set; }
        public string? IncomeTaxRegime { get; set; }

        // Solo aplica cuando PartyType = Empleado (requeridos por Dataico para Nómina Electrónica)
        public string? WorkerType { get; set; }
        public string? ContractType { get; set; }
        public string? PaymentMeans { get; set; }
        public string? Bank { get; set; }
        public string? AccountType { get; set; }
        public string? AccountNumber { get; set; }
        public bool HighRisk { get; set; }
        public bool IntegralSalary { get; set; }
        public decimal? BaseSalary { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? FireDate { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
