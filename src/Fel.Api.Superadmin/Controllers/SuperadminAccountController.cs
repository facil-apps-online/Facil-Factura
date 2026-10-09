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

namespace Fel.Api.Superadmin.Controllers
{
    // Perfil y sesión del superadmin (ver AccountControllerBase). Su sesión admite como máximo 2 horas de inactividad (SessionPolicy).
    [Route("api/superadmin/auth")]
    public class SuperadminAccountController : AccountControllerBase
    {
        private readonly FelDbContext _dbContext;

        public SuperadminAccountController(AccountSessionService sessions, FelDbContext dbContext) : base(sessions) => _dbContext = dbContext;

        protected override PortalKind Kind => PortalKind.Superadmin;

        protected override async Task<ISessionAccount?> FindAccountAsync(ClaimsPrincipal user) =>
            Guid.TryParse(user.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id)
                ? await _dbContext.SuperadminUsers.FirstOrDefaultAsync(u => u.Id == id)
                : null;

        protected override Task SaveAsync() => _dbContext.SaveChangesAsync();
        protected override bool VerifyPassword(string password, string hash) => SuperadminPasswordHasher.Verify(password, hash);
        protected override string HashPassword(string password) => SuperadminPasswordHasher.Hash(password);

        protected override Task<(string Role, string? Organization)> DescribeAsync(ISessionAccount account) =>
            Task.FromResult(("Superadmin", (string?)"Facil Factura"));
    }
}
