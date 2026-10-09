using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Fel.Api.Shared;
using Fel.Core.Entities;
using Fel.Core.Security;
using Fel.Infrastructure.Data;
using Fel.Infrastructure.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Fel.Api.Tenant.Controllers
{
    // Perfil y sesión del usuario del portal del tenant (ver AccountControllerBase).
    [Route("api/tenant/auth")]
    public class TenantAccountController : AccountControllerBase
    {
        private readonly FelDbContext _dbContext;

        public TenantAccountController(AccountSessionService sessions, FelDbContext dbContext) : base(sessions) => _dbContext = dbContext;

        protected override PortalKind Kind => PortalKind.Tenant;

        protected override async Task<ISessionAccount?> FindAccountAsync(ClaimsPrincipal user) =>
            Guid.TryParse(user.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id)
                ? await _dbContext.TenantUsers.FirstOrDefaultAsync(u => u.Id == id && u.IsActive)
                : null;

        protected override Task SaveAsync() => _dbContext.SaveChangesAsync();
        protected override bool VerifyPassword(string password, string hash) => BCrypt.Net.BCrypt.Verify(password, hash);
        protected override string HashPassword(string password) => BCrypt.Net.BCrypt.HashPassword(password);

        protected override async Task<(string Role, string? Organization)> DescribeAsync(ISessionAccount account)
        {
            Guid.TryParse(User.FindFirst("TenantId")?.Value, out var tenantId);
            var name = await _dbContext.Tenants.AsNoTracking().Where(t => t.Id == tenantId)
                .Select(t => !string.IsNullOrEmpty(t.CommercialName) ? t.CommercialName : (!string.IsNullOrEmpty(t.LegalName) ? t.LegalName : t.Name)).FirstOrDefaultAsync();
            return ("Usuario del tenant", name);
        }

        // Renovar solo mientras la cuenta siga con acceso vigente al tenant del token (puede habérselo revocado en el ínterin).
        protected override async Task<bool> CanRenewAsync(ISessionAccount account, ClaimsPrincipal user) =>
            Guid.TryParse(user.FindFirst("TenantId")?.Value, out var tenantId)
            && await _dbContext.TenantUserAssignments.AnyAsync(a => a.TenantUserId == account.Id && a.TenantId == tenantId && a.IsActive && a.Tenant.IsActive);
    }
}
