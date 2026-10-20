using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Fel.Core.Entities;
using Fel.Core.Interfaces;
using Fel.Core.Security;
using Fel.Infrastructure.Data;
using Fel.Infrastructure.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Fel.Api.Tenant.Controllers
{
    // Modo soporte: un usuario del tenant entra al portal de uno de sus clientes (para parametrizar o probar en su nombre) sin que el cliente
    // tenga que darle un acceso. Se emite una sesión del portal de clientes con permisos de administrador y todas las sucursales, que vive del
    // sello del usuario del tenant (si le cambian la contraseña o le quitan el acceso, cae sola) y deja constancia en SupportAccesses.
    [ApiController]
    [Route("api/tenant/clients/{clientId:guid}/support-session")]
    public class TenantSupportController : ControllerBase
    {
        private readonly FelDbContext _dbContext;
        private readonly ISessionTokenService _tokens;

        public TenantSupportController(FelDbContext dbContext, ISessionTokenService tokens)
        {
            _dbContext = dbContext;
            _tokens = tokens;
        }

        private Guid GetCurrentTenantId()
        {
            if (Request.Headers.TryGetValue("x-tenant-id", out var tenantIdStr) && Guid.TryParse(tenantIdStr, out var tenantId))
                return tenantId;
            throw new UnauthorizedAccessException("x-tenant-id Header is missing");
        }

        [HttpPost]
        public async Task<IActionResult> Start(Guid clientId)
        {
            var tenantId = GetCurrentTenantId();
            if (!Guid.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var userId)) return Unauthorized();

            var user = await _dbContext.TenantUsers.FirstOrDefaultAsync(u => u.Id == userId && u.IsActive);
            var hasAccess = user != null && await _dbContext.TenantUserAssignments
                .AnyAsync(a => a.TenantUserId == userId && a.TenantId == tenantId && a.IsActive && a.Tenant.IsActive);
            if (!hasAccess) return Unauthorized();

            var client = await _dbContext.Clients.AsNoTracking()
                .Where(c => c.Id == clientId && c.TenantId == tenantId)
                .Select(c => new { c.Id, c.IsActive, c.IsDeveloperSandbox, Name = !string.IsNullOrEmpty(c.CommercialName) ? c.CommercialName : c.CompanyName })
                .FirstOrDefaultAsync();
            if (client == null) return NotFound("Cliente no encontrado.");
            if (!client.IsActive) return BadRequest("El cliente está inactivo.");

            // El bloqueo administrativo manda: con el tenant bloqueado o suspendido sus clientes no entran, ni siquiera en soporte.
            if ((await TenantBlocks.GetAsync(_dbContext, tenantId)).Level != TenantBlockLevel.None)
                return StatusCode(403, "El tenant está suspendido: no se puede entrar al portal de sus clientes.");

            var tenantSlug = await _dbContext.Tenants.AsNoTracking().Where(t => t.Id == tenantId).Select(t => t.Slug).FirstOrDefaultAsync();

            var minutes = SessionPolicy.Effective(PortalKind.Client, user!.SessionMinutes);
            var session = _tokens.IssueSession(new[]
            {
                ("ClientId", client.Id.ToString()),
                ("TenantId", tenantId.ToString()),
                (SessionStampValidator.SupportClaim, user.Id.ToString()),
                (ClaimTypes.NameIdentifier, user.Id.ToString()),
                (ClaimTypes.Email, user.Email)
            }, user.SecurityStamp, minutes, DateTime.UtcNow);

            _dbContext.SupportAccesses.Add(new SupportAccess
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                TenantUserId = user.Id,
                TenantUserEmail = user.Email,
                ClientId = client.Id,
                StartedAt = DateTime.UtcNow
            });
            await _dbContext.SaveChangesAsync();

            return Ok(new
            {
                token = session.Token,
                clientId = client.Id,
                clientName = client.Name,
                tenantSlug,
                userName = string.IsNullOrWhiteSpace(user.Name) ? user.Email : user.Name
            });
        }
    }
}
