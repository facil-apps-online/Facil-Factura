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
    // Perfil y sesión del usuario del portal del cliente (ver AccountControllerBase).
    [Route("api/client/auth")]
    public class ClientAccountController : AccountControllerBase
    {
        private readonly FelDbContext _dbContext;

        public ClientAccountController(AccountSessionService sessions, FelDbContext dbContext) : base(sessions) => _dbContext = dbContext;

        protected override PortalKind Kind => PortalKind.Client;

        protected override async Task<ISessionAccount?> FindAccountAsync(ClaimsPrincipal user) =>
            Guid.TryParse(user.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id)
                ? await _dbContext.ClientUsers.Include(u => u.Client).FirstOrDefaultAsync(u => u.Id == id && u.IsActive)
                : null;

        protected override Task SaveAsync() => _dbContext.SaveChangesAsync();
        protected override bool VerifyPassword(string password, string hash) => BCrypt.Net.BCrypt.Verify(password, hash);
        protected override string HashPassword(string password) => BCrypt.Net.BCrypt.HashPassword(password);

        protected override Task<(string Role, string? Organization)> DescribeAsync(ISessionAccount account)
        {
            var user = (ClientUser)account;
            var company = string.IsNullOrEmpty(user.Client.CommercialName) ? user.Client.CompanyName : user.Client.CommercialName;
            return Task.FromResult((user.Role, (string?)company));
        }

        // Renovar solo mientras el cliente siga activo.
        protected override Task<bool> CanRenewAsync(ISessionAccount account, ClaimsPrincipal user) => Task.FromResult(((ClientUser)account).Client.IsActive);
    }
}
