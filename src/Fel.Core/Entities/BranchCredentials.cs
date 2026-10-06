using System;

namespace Fel.Core.Entities
{
    // Credenciales propias de una sucursal. Cada grupo es una fila opcional (1:1 con la sucursal): sin fila, la sucursal usa las de la
    // sucursal principal, que son el valor por defecto de las demás (nada se copia, así que no se desactualiza). Con fila, ésta reemplaza por
    // completo a la de la principal para esa sucursal. Las claves van cifradas (ICryptoService) y nunca se serializan.

    // Prestador ante el MUV-FEV-RIPS (LoginSISPRO).
    public class BranchMinSaludCredential
    {
        public Guid BranchId { get; set; }
        public Branch Branch { get; set; } = null!;

        public string Environment { get; set; } = MinSaludEnvironments.Test;
        public string? UserType { get; set; }

        public string? IdentificationType { get; set; }
        public string? IdentificationNumber { get; set; }
        [System.Text.Json.Serialization.JsonIgnore]
        public string? PasswordEncrypted { get; set; }

        public string? TestIdentificationType { get; set; }
        public string? TestIdentificationNumber { get; set; }
        [System.Text.Json.Serialization.JsonIgnore]
        public string? TestPasswordEncrypted { get; set; }
    }

    // Llaves del Client ante IHCE (Interoperabilidad de Historia Clínica Electrónica).
    public class BranchIhceCredential
    {
        public Guid BranchId { get; set; }
        public Branch Branch { get; set; } = null!;

        public string? ClientId { get; set; }
        [System.Text.Json.Serialization.JsonIgnore]
        public string? ClientSecretEncrypted { get; set; }
        [System.Text.Json.Serialization.JsonIgnore]
        public string? ApimSubscriptionKeyEncrypted { get; set; }
        public string? TenantId { get; set; }
        public string? Endpoint { get; set; }
        public string Environment { get; set; } = "Sandbox"; // Sandbox (Preproducción) o Production
    }

    // Eventos RADIAN que se crean solos al recibir un documento en esta sucursal. Sin fila usa los de la sucursal principal.
    public class BranchReceptionEvents
    {
        public Guid BranchId { get; set; }
        public Branch Branch { get; set; } = null!;

        public bool AutoSendAcuseRecibo { get; set; }
        public bool AutoSendReciboBien { get; set; }
        public bool AutoSendAceptacion { get; set; }
        public bool AutoSendReclamo { get; set; }
    }

    // Buzón IMAP donde esta sucursal recibe las facturas de sus proveedores: lo que llega a él queda en la sucursal.
    public class BranchReceptionMailbox
    {
        public Guid BranchId { get; set; }
        public Branch Branch { get; set; } = null!;

        public bool Enabled { get; set; }
        public string Host { get; set; } = string.Empty;
        public int Port { get; set; } = 993;
        public bool UseSsl { get; set; } = true;
        public string User { get; set; } = string.Empty;
        [System.Text.Json.Serialization.JsonIgnore]
        public string PasswordEncrypted { get; set; } = string.Empty;
    }
}
