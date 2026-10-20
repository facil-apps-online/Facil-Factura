using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Fel.Api.Shared;
using Fel.Core.Entities;
using Fel.Core.Security;
using Fel.Infrastructure.Data;
using Fel.Infrastructure.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Fel.Api.Client.Controllers
{
    // Perfil y sesión del usuario del portal del cliente (ver AccountControllerBase).
    [Route("api/client/auth")]
    public class ClientAccountController : AccountControllerBase
    {
        private readonly FelDbContext _dbContext;

        public ClientAccountController(AccountSessionService sessions, FelDbContext dbContext) : base(sessions) => _dbContext = dbContext;

        protected override PortalKind Kind => PortalKind.Client;

        // Sesión de soporte: la cuenta es la del usuario del tenant, no un usuario del cliente (ver SessionStampValidator.SupportClaim).
        private static bool IsSupport(ClaimsPrincipal user) => user.HasClaim(c => c.Type == SessionStampValidator.SupportClaim);

        protected override async Task<ISessionAccount?> FindAccountAsync(ClaimsPrincipal user)
        {
            if (IsSupport(user))
            {
                return Guid.TryParse(user.FindFirst(SessionStampValidator.SupportClaim)?.Value, out var supportId)
                    ? await _dbContext.TenantUsers.FirstOrDefaultAsync(u => u.Id == supportId && u.IsActive)
                    : null;
            }

            return Guid.TryParse(user.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id)
                ? await _dbContext.ClientUsers.Include(u => u.Client).FirstOrDefaultAsync(u => u.Id == id && u.IsActive)
                : null;
        }

        protected override Task SaveAsync() => _dbContext.SaveChangesAsync();
        protected override bool VerifyPassword(string password, string hash) => BCrypt.Net.BCrypt.Verify(password, hash);
        protected override string HashPassword(string password) => BCrypt.Net.BCrypt.HashPassword(password);

        protected override async Task<(string Role, string? Organization)> DescribeAsync(ISessionAccount account)
        {
            if (account is TenantUser)
            {
                Guid.TryParse(User.FindFirst("ClientId")?.Value, out var supportClientId);
                var companyName = await _dbContext.Clients.AsNoTracking().Where(c => c.Id == supportClientId)
                    .Select(c => !string.IsNullOrEmpty(c.CommercialName) ? c.CommercialName : c.CompanyName).FirstOrDefaultAsync();
                return ("Soporte del tenant", companyName);
            }

            var user = (ClientUser)account;
            var company = string.IsNullOrEmpty(user.Client.CommercialName) ? user.Client.CompanyName : user.Client.CommercialName;
            return (user.Role, company);
        }

        // Renovar solo mientras el cliente siga activo. En soporte, además, mientras el usuario del tenant conserve acceso vigente a ese cliente
        // (el bloqueo administrativo lo corta el validador de sesiones).
        protected override async Task<bool> CanRenewAsync(ISessionAccount account, ClaimsPrincipal user)
        {
            if (account is ClientUser clientUser) return clientUser.Client.IsActive;

            return Guid.TryParse(user.FindFirst("ClientId")?.Value, out var clientId)
                && await _dbContext.Clients.AnyAsync(c => c.Id == clientId && c.IsActive
                    && _dbContext.TenantUserAssignments.Any(a => a.TenantUserId == account.Id && a.TenantId == c.TenantId && a.IsActive && a.Tenant.IsActive));
        }
    }
}
