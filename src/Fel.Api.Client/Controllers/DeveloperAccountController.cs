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

namespace Fel.Api.Client.Controllers
{
    // Perfil y sesión del usuario del portal de developers (ver AccountControllerBase).
    [Route("api/developer/auth")]
    public class DeveloperAccountController : AccountControllerBase
    {
        private readonly FelDbContext _dbContext;

        public DeveloperAccountController(AccountSessionService sessions, FelDbContext dbContext) : base(sessions) => _dbContext = dbContext;

        protected override PortalKind Kind => PortalKind.Developer;

        protected override async Task<ISessionAccount?> FindAccountAsync(ClaimsPrincipal user) =>
            Guid.TryParse(user.FindFirst("DeveloperId")?.Value, out var id)
                ? await _dbContext.DeveloperUsers.Include(u => u.Tenant).FirstOrDefaultAsync(u => u.Id == id && u.IsActive)
                : null;

        protected override Task SaveAsync() => _dbContext.SaveChangesAsync();
        protected override bool VerifyPassword(string password, string hash) => BCrypt.Net.BCrypt.Verify(password, hash);
        protected override string HashPassword(string password) => BCrypt.Net.BCrypt.HashPassword(password);

        protected override Task<(string Role, string? Organization)> DescribeAsync(ISessionAccount account)
        {
            var tenant = ((DeveloperUser)account).Tenant;
            return Task.FromResult(("Developer", tenant == null ? null : (!string.IsNullOrEmpty(tenant.CommercialName) ? tenant.CommercialName : (!string.IsNullOrEmpty(tenant.LegalName) ? tenant.LegalName : tenant.Name))));
        }
    }
}
