using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Fel.Core.Entities;
using Fel.Infrastructure.Data;
using Fel.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace Fel.Api.Client.Security
{
    // Arma la sesión del portal para un usuario ya autenticado: la usan el inicio de sesión y el cambio de empresa de quien tiene el mismo
    // correo en varios clientes del tenant (un contador, por ejemplo). Las comprobaciones son las mismas en los dos caminos.
    public class ClientSessionIssuer
    {
        private readonly FelDbContext _dbContext;
        private readonly AccountSessionService _accountSessions;

        public ClientSessionIssuer(FelDbContext dbContext, AccountSessionService accountSessions)
        {
            _dbContext = dbContext;
            _accountSessions = accountSessions;
        }

        public sealed record IssueResult(int Status, string? Error, object? Payload)
        {
            public bool Ok => Payload != null;
        }

        // Empresas (clientes) a las que puede entrar quien tiene este correo en este tenant: activas y sin bloqueo. El usuario debe estar activo.
        public async Task<List<ClientUser>> CompaniesOfAsync(Guid tenantId, string email) =>
            await _dbContext.ClientUsers.Include(u => u.Client)
                .Where(u => u.Email == email && u.Client.TenantId == tenantId && u.IsActive && u.Client.IsActive && u.Client.BlockedAt == null)
                .OrderBy(u => u.Client.CommercialName).ThenBy(u => u.Client.CompanyName)
                .ToListAsync();

        public static string CompanyName(Fel.Core.Entities.Client client) =>
            string.IsNullOrWhiteSpace(client.CommercialName) ? client.CompanyName : client.CommercialName;

        public async Task<IssueResult> IssueAsync(ClientUser user, Tenant tenant)
        {
            // Bloqueo administrativo del tenant (o de un ancestro): el cliente no entra. Mensaje neutro, sin motivo.
            if ((await TenantBlocks.GetAsync(_dbContext, tenant.Id)).Level != TenantBlockLevel.None)
                return new IssueResult(403, TenantBlocks.ClientMessage, null);
            // Bloqueo del propio cliente por su tenant.
            if (user.Client.BlockedAt != null) return new IssueResult(403, TenantBlocks.ClientMessage, null);

            // Sucursales a las que puede entrar el usuario (la principal primero). Sin ninguna activa no hay sesión útil.
            var branchesQuery = _dbContext.Branches.AsNoTracking().Where(b => b.ClientId == user.ClientId && b.IsActive);
            if (!user.AllBranches)
            {
                var assigned = await _dbContext.ClientUserBranches.Where(ub => ub.ClientUserId == user.Id).Select(ub => ub.BranchId).ToListAsync();
                branchesQuery = branchesQuery.Where(b => assigned.Contains(b.Id));
            }
            var branches = await branchesQuery
                .OrderByDescending(b => b.IsMain).ThenBy(b => b.Name)
                .Select(b => new { b.Id, b.Name, b.Code, b.IsMain })
                .ToListAsync();
            if (branches.Count == 0)
                return new IssueResult(401, "No tienes sucursales activas asignadas. Contacta a tu administrador.", null);

            var token = _accountSessions.Issue(Fel.Core.Security.PortalKind.Client, user, new[]
            {
                ("ClientId", user.ClientId.ToString()),
                (System.Security.Claims.ClaimTypes.NameIdentifier, user.Id.ToString()),
                (System.Security.Claims.ClaimTypes.Email, user.Email)
            }).Token;

            return new IssueResult(200, null, new
            {
                token,
                clientId = user.ClientId,
                name = user.Name,
                email = user.Email,
                companyName = user.Client.CommercialName,
                tenantSlug = tenant.Slug,
                role = user.Role,
                allBranches = user.AllBranches,
                branches
            });
        }
    }
}
