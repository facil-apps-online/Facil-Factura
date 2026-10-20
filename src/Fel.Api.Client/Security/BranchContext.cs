using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Fel.Core.Entities;
using Fel.Infrastructure.Data;
using Fel.Infrastructure.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Fel.Api.Security
{
    // Quién hace la petición del portal de clientes y sobre qué sucursal: el Client viene del header x-client-id (que
    // SessionHeaderGuardMiddleware ya validó contra el token), la sucursal del header opcional x-branch-id y el rol y las
    // sucursales permitidas se leen de la base en cada petición, para que un cambio de rol o de acceso aplique de inmediato
    // y no recién cuando venza el token.
    public sealed class BranchContext
    {
        public const string ItemKey = "Fel.BranchContext";
        public const string BranchHeader = "x-branch-id";
        public const string AllBranchesValue = "all";

        public Guid ClientId { get; init; }
        public Guid UserId { get; init; }
        public string Role { get; init; } = ClientUserRoles.Invoicer;
        public bool IsAdministrator => Role == ClientUserRoles.Administrator;
        public bool CanViewAllBranches { get; init; }

        // Sucursal activa de la petición. Null = "todas las sucursales", solo válido para lecturas.
        public Guid? BranchId { get; init; }
        public IReadOnlyList<Guid> AllowedBranchIds { get; init; } = Array.Empty<Guid>();

        // Para crear o emitir: exige una sucursal concreta.
        public Guid RequireBranchId() =>
            BranchId ?? throw new BranchAccessException(StatusCodes.Status400BadRequest, "Selecciona una sucursal para esta operación.");

        public static async Task<BranchContext> ResolveAsync(HttpContext http, FelDbContext dbContext)
        {
            if (!http.Request.Headers.TryGetValue("x-client-id", out var clientIdHeader) || !Guid.TryParse(clientIdHeader, out var clientId))
                throw new BranchAccessException(StatusCodes.Status401Unauthorized, "x-client-id Header is missing");

            var userIdValue = http.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? http.User?.FindFirst("nameid")?.Value;
            if (!Guid.TryParse(userIdValue, out var userId))
                throw new BranchAccessException(StatusCodes.Status401Unauthorized, "Sesión inválida o expirada. Vuelve a iniciar sesión.");

            // Sesión de soporte: un usuario del tenant dentro del portal de un cliente de su tenant. No hay fila de ClientUsers: entra como
            // administrador con todas las sucursales, y se comprueba en cada petición que el cliente siga activo y que su acceso al tenant siga vigente.
            Actor? user;
            if (http.User?.HasClaim(c => c.Type == SessionStampValidator.SupportClaim) == true)
            {
                var supportUserId = http.User.FindFirst(SessionStampValidator.SupportClaim)?.Value;
                var tokenClientId = http.User.FindFirst("ClientId")?.Value;
                if (!Guid.TryParse(supportUserId, out var supportId) || !string.Equals(tokenClientId, clientId.ToString(), StringComparison.OrdinalIgnoreCase))
                    throw new BranchAccessException(StatusCodes.Status401Unauthorized, "Sesión inválida o expirada. Vuelve a iniciar sesión.");

                var allowedSupport = await dbContext.Clients.AsNoTracking()
                    .Where(c => c.Id == clientId && c.IsActive)
                    .AnyAsync(c => dbContext.TenantUsers.Any(u => u.Id == supportId && u.IsActive)
                                   && dbContext.TenantUserAssignments.Any(a => a.TenantUserId == supportId && a.TenantId == c.TenantId && a.IsActive && a.Tenant.IsActive));
                user = allowedSupport ? new Actor(clientId, true, ClientUserRoles.Administrator, true, new List<Guid>()) : null;
                userId = supportId;
            }
            else
            {
                user = await dbContext.ClientUsers.AsNoTracking()
                    .Where(u => u.Id == userId)
                    .Select(u => new Actor(u.ClientId, u.IsActive, u.Role, u.AllBranches, u.Branches.Select(b => b.BranchId).ToList()))
                    .FirstOrDefaultAsync();
            }
            if (user == null || !user.IsActive || user.ClientId != clientId)
                throw new BranchAccessException(StatusCodes.Status401Unauthorized, "Sesión inválida o expirada. Vuelve a iniciar sesión.");

            // La principal primero: es la sucursal por defecto cuando no llega x-branch-id.
            var branches = await dbContext.Branches.AsNoTracking()
                .Where(b => b.ClientId == clientId && b.IsActive)
                .OrderByDescending(b => b.IsMain).ThenBy(b => b.Name)
                .Select(b => b.Id)
                .ToListAsync();
            var allowed = user.AllBranches ? branches : branches.Where(user.Assigned.Contains).ToList();
            if (allowed.Count == 0)
                throw new BranchAccessException(StatusCodes.Status403Forbidden, "No tienes sucursales activas asignadas.");

            Guid? branchId;
            var requested = http.Request.Headers[BranchHeader].ToString();
            if (string.IsNullOrWhiteSpace(requested))
            {
                branchId = allowed[0];
            }
            else if (string.Equals(requested, AllBranchesValue, StringComparison.OrdinalIgnoreCase))
            {
                if (!user.AllBranches)
                    throw new BranchAccessException(StatusCodes.Status403Forbidden, "No tienes acceso a todas las sucursales.");
                branchId = null;
            }
            else if (Guid.TryParse(requested, out var requestedId) && allowed.Contains(requestedId))
            {
                branchId = requestedId;
            }
            else
            {
                throw new BranchAccessException(StatusCodes.Status403Forbidden, "No tienes acceso a esa sucursal.");
            }

            return new BranchContext
            {
                ClientId = clientId,
                UserId = userId,
                Role = ClientUserRoles.IsValid(user.Role) ? user.Role : ClientUserRoles.Invoicer,
                CanViewAllBranches = user.AllBranches,
                BranchId = branchId,
                AllowedBranchIds = allowed
            };
        }
    }

    // Quien hace la petición, ya sea un usuario del cliente o un usuario del tenant en modo soporte.
    internal sealed record Actor(Guid ClientId, bool IsActive, string Role, bool AllBranches, List<Guid> Assigned);

    public sealed class BranchAccessException : Exception
    {
        public int StatusCode { get; }

        public BranchAccessException(int statusCode, string message) : base(message)
        {
            StatusCode = statusCode;
        }
    }
}
