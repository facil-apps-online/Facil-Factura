using System;
using System.Threading.Tasks;
using Fel.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Fel.Api.Client.Controllers
{
    [ApiController]
    [Route("api/developer")]
    public class DeveloperController : ControllerBase
    {
        private readonly FelDbContext _dbContext;

        public DeveloperController(FelDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        private Guid GetCurrentDeveloperId()
        {
            if (Request.Headers.TryGetValue("x-developer-id", out var idStr) && Guid.TryParse(idStr, out var id))
            {
                return id;
            }
            throw new UnauthorizedAccessException("x-developer-id Header is missing");
        }

        // Perfil del developer autenticado, con sus credenciales de prueba cuando tiene un Client
        // propio (registro independiente). Un developer invitado por un Tenant (TenantId con
        // valor, ClientId null) no tiene credenciales propias aquí todavía — eso lo cubre la
        // Fase 2 (ver las credenciales de los Clients del Tenant que lo invitó).
        [HttpGet("me")]
        public async Task<IActionResult> GetMe()
        {
            try
            {
                var developerId = GetCurrentDeveloperId();
                var developer = await _dbContext.DeveloperUsers
                    .Include(d => d.Tenant)
                    .Include(d => d.Client)
                    .FirstOrDefaultAsync(d => d.Id == developerId && d.IsActive);

                if (developer == null) return NotFound();

                var sandboxBranch = developer.ClientId == null ? null : await _dbContext.Branches.AsNoTracking()
                    .FirstOrDefaultAsync(b => b.ClientId == developer.ClientId && b.IsMain);

                return Ok(new
                {
                    developer.Id,
                    developer.Name,
                    developer.Email,
                    Tenant = developer.Tenant == null ? null : new { developer.Tenant.Id, developer.Tenant.CommercialName },
                    SandboxCredentials = developer.Client == null ? null : new
                    {
                        developer.Client.Id,
                        developer.Client.CompanyName,
                        sandboxBranch?.TestApiKey,
                        sandboxBranch?.TestApiSecret,
                        developer.Client.DataicoEnvironment
                    }
                });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(ex.Message);
            }
        }
    }
}
