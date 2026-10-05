using System;
using System.Collections.Generic;

namespace Fel.Core.Entities
{
    // Roles del portal de clientes: el Administrador configura y emite; el Facturador solo emite y consulta los
    // documentos de sus sucursales.
    public static class ClientUserRoles
    {
        public const string Administrator = "Administrador";
        public const string Invoicer = "Facturador";

        public static bool IsValid(string? role) => role == Administrator || role == Invoicer;
    }

    public class ClientUser
    {
        public Guid Id { get; set; }
        public Guid ClientId { get; set; }
        public Client Client { get; set; } = null!;

        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        [System.Text.Json.Serialization.JsonIgnore]
        public string PasswordHash { get; set; } = string.Empty;

        public string Role { get; set; } = ClientUserRoles.Administrator;
        // true = ve todas las sucursales del Client; false = solo las de ClientUserBranch.
        public bool AllBranches { get; set; } = true;
        public ICollection<ClientUserBranch> Branches { get; set; } = new List<ClientUserBranch>();

        public DateTime CreatedAt { get; set; }
        public bool IsActive { get; set; }
    }
}
