using System;
using System.Collections.Generic;
using System.Linq;

namespace Fel.Core.Entities
{
    // Roles del portal de clientes: el Administrador configura y emite; el Facturador solo emite y consulta los
    // documentos de sus sucursales.
    public static class ClientUserRoles
    {
        public const string Administrator = "Administrador";
        public const string Invoicer = "Facturador";

        // Catálogo único de roles (valor y etiqueta): la API lo expone para que el portal no los escriba a mano.
        public static readonly IReadOnlyList<(string Value, string Label)> All = new[]
        {
            (Administrator, "Administrador"),
            (Invoicer, "Facturador")
        };

        public static bool IsValid(string? role) => All.Any(r => r.Value == role);
    }

    public class ClientUser : ISessionAccount
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

        // Sesión del portal: sello de seguridad (ver ISessionAccount) y minutos de inactividad que elige el usuario.
        public Guid SecurityStamp { get; set; } = Guid.NewGuid();
        public int SessionMinutes { get; set; } = Fel.Core.Security.SessionPolicy.DefaultMinutes;
    }
}
