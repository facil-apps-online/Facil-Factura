using System;
using Fel.Api.Security;
using Microsoft.AspNetCore.Mvc;

namespace Fel.Api.Client.Controllers
{
    // Base de los controladores del portal de clientes: ClientPortalFilter resuelve cliente, sucursal y rol antes de cada
    // acción (y corta con 401/403 si la sesión o el rol no corresponden), así que acá solo se leen ya resueltos.
    [ServiceFilter(typeof(ClientPortalFilter))]
    public abstract class ClientPortalControllerBase : ControllerBase
    {
        protected BranchContext Branch =>
            HttpContext.Items[BranchContext.ItemKey] as BranchContext
            ?? throw new UnauthorizedAccessException("Sesión inválida o expirada. Vuelve a iniciar sesión.");

        protected Guid GetCurrentClientId() => Branch.ClientId;

        // Sucursal por la que se filtran las consultas. Null = todas las sucursales del usuario.
        protected Guid? CurrentBranchScope => Branch.BranchId;

        // Sucursal concreta para crear o emitir.
        protected Guid GetCurrentBranchId() => Branch.RequireBranchId();
    }
}
