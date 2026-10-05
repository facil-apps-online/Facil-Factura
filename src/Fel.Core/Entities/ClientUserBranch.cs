using System;

namespace Fel.Core.Entities
{
    // Sucursales a las que tiene acceso un usuario del portal de clientes (cuando no tiene AllBranches).
    public class ClientUserBranch
    {
        public Guid ClientUserId { get; set; }
        public ClientUser ClientUser { get; set; } = null!;

        public Guid BranchId { get; set; }
        public Branch Branch { get; set; } = null!;
    }
}
