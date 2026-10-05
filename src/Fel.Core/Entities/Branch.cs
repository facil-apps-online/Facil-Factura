using System;

namespace Fel.Core.Entities
{
    // Sucursal de un Client. Todo Client tiene una sucursal principal (IsMain) que agrupa lo que existía antes de que
    // hubiera sucursales. Los documentos, usuarios y demás datos operativos se aíslan por sucursal; los productos,
    // terceros y plantillas son del Client y se comparten entre sus sucursales. CreatedAt es la fecha desde la que se
    // cobra la sucursal mientras esté activa.
    public class Branch
    {
        public Guid Id { get; set; }
        public Guid ClientId { get; set; }
        public Client Client { get; set; } = null!;

        public string Name { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public bool IsMain { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; }
        // Cuándo se desactivó (nulo si está activa). El cobro por sucursal activa prorratea los días hasta esta fecha.
        public DateTime? DeactivatedAt { get; set; }

        // --- Tarifa del Tenant a su Client por esta sucursal (clientes con Dataico) ---
        // Cuota fija mensual y precio por documento. Las bolsas y planes prepago son del Client y las comparten todas sus sucursales.
        public decimal SubscriptionRate { get; set; }
        public decimal PricePerDocument { get; set; }

        // Ubicación del emisor para los documentos de esta sucursal. Todo opcional: sin Address la sucursal usa la del Client
        // (la principal hereda, así no se desactualiza cuando se edita la dirección del Client). Ver EmitterLocation.
        public string? Address { get; set; }
        public string? City { get; set; }
        public string? CityCode { get; set; } // Código DANE del municipio (5 dígitos)
        public string? Phone { get; set; }
        public string? Email { get; set; }

        // --- API de integración (HMAC): las llaves son de la sucursal, no del Client ---
        [System.Text.Json.Serialization.JsonIgnore]
        public string LiveApiKey { get; set; } = Guid.NewGuid().ToString("N");
        [System.Text.Json.Serialization.JsonIgnore]
        public string LiveApiSecret { get; set; } = Guid.NewGuid().ToString("N");
        [System.Text.Json.Serialization.JsonIgnore]
        public string TestApiKey { get; set; } = "test_" + Guid.NewGuid().ToString("N");
        [System.Text.Json.Serialization.JsonIgnore]
        public string TestApiSecret { get; set; } = Guid.NewGuid().ToString("N");

        public const string MainName = "Principal";
        public const string MainCode = "PRINCIPAL";
    }
}
