using System;
using System.Threading.Tasks;
using Fel.Core.Entities;
using Fel.Core.Interfaces;
using Fel.Infrastructure.Data;
using Fel.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Fel.Api.Client.Controllers
{
    [ApiController]
    [Route("api/client/auth")]
    [AllowAnonymous]
    public class ClientAuthController : ControllerBase
    {
        private readonly FelDbContext _dbContext;
        private readonly PasswordResetService _passwordResetService;
        private readonly Fel.Infrastructure.Security.AccountSessionService _accountSessions;
        private readonly string _portalUrl;
        private readonly Fel.Api.Client.Security.ClientSessionIssuer _sessionIssuer;

        public ClientAuthController(FelDbContext dbContext, PasswordResetService passwordResetService, Fel.Infrastructure.Security.AccountSessionService accountSessions, Fel.Api.Client.Security.ClientSessionIssuer sessionIssuer, IConfiguration config)
        {
            _dbContext = dbContext;
            _passwordResetService = passwordResetService;
            _accountSessions = accountSessions;
            _sessionIssuer = sessionIssuer;
            _portalUrl = config["PortalUrl"] ?? "https://clients.facil-factura.pro";
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] ClientLoginRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.TenantSlug))
            {
                return BadRequest("Falta identificar la empresa (slug).");
            }

            var tenant = await _dbContext.Tenants
                .FirstOrDefaultAsync(t => t.Slug == request.TenantSlug && t.IsActive);

            if (tenant == null)
            {
                return Unauthorized("Empresa no encontrada o inactiva.");
            }

            // Un mismo correo puede tener acceso a varias empresas (clientes) del tenant: un contador que lleva varios. Una sola contraseña
            // para todas (se fija en cada usuario del grupo al establecerla), y si son varias la persona elige a cuál entra.
            var email = (request.Email ?? string.Empty).Trim();
            var members = await _dbContext.ClientUsers
                .Include(u => u.Client)
                .Where(u => u.Email == email && u.Client.TenantId == tenant.Id && u.IsActive && u.Client.IsActive)
                .ToListAsync();

            if (members.Count == 0)
            {
                return Unauthorized("Credenciales incorrectas o usuario inactivo.");
            }

            // Basta con que la contraseña coincida con la de cualquier usuario del grupo (antes de la primera vez que la establece, cada uno tiene la suya aleatoria).
            if (!members.Any(u => BCrypt.Net.BCrypt.Verify(request.Password, u.PasswordHash)))
            {
                return Unauthorized("Credenciales incorrectas.");
            }

            // Bloqueo administrativo del tenant (o de un ancestro): el cliente no entra. Mensaje neutro, sin motivo.
            if ((await Fel.Infrastructure.Security.TenantBlocks.GetAsync(_dbContext, tenant.Id)).Level != Fel.Infrastructure.Security.TenantBlockLevel.None)
            {
                return StatusCode(403, Fel.Infrastructure.Security.TenantBlocks.ClientMessage);
            }

            // Empresas a las que puede entrar (sin las bloqueadas por su tenant).
            var open = members.Where(u => u.Client.BlockedAt == null).ToList();
            if (open.Count == 0) return StatusCode(403, Fel.Infrastructure.Security.TenantBlocks.ClientMessage);

            ClientUser? user;
            if (request.ClientId.HasValue)
            {
                user = open.FirstOrDefault(u => u.ClientId == request.ClientId.Value);
                if (user == null) return Unauthorized("No tienes acceso a esa empresa.");
            }
            else if (open.Count == 1)
            {
                user = open[0];
            }
            else
            {
                // Varias empresas: no hay sesión todavía; el portal pide elegir una y repite el inicio de sesión con su clientId.
                return Ok(new
                {
                    requiresCompany = true,
                    companies = open
                        .OrderBy(u => Fel.Api.Client.Security.ClientSessionIssuer.CompanyName(u.Client))
                        .Select(u => new { clientId = u.ClientId, name = Fel.Api.Client.Security.ClientSessionIssuer.CompanyName(u.Client), taxId = u.Client.TaxId })
                });
            }

            var issued = await _sessionIssuer.IssueAsync(user, tenant);
            if (!issued.Ok) return StatusCode(issued.Status, issued.Error);
            return Ok(issued.Payload);
        }
        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] ClientForgotPasswordRequest request)
        {
            // Siempre responde igual exista o no la cuenta, para no filtrar qué emails están
            // registrados (evita enumeración de usuarios).
            const string genericMessage = "Si el correo está registrado, te enviamos un enlace para restablecer tu contraseña.";

            var tenant = await _dbContext.Tenants.FirstOrDefaultAsync(t => t.Slug == request.TenantSlug && t.IsActive);
            if (tenant == null) return Ok(new { message = genericMessage });

            var user = await _dbContext.ClientUsers
                .FirstOrDefaultAsync(u => u.Email == request.Email && u.Client.TenantId == tenant.Id && u.IsActive);
            if (user == null) return Ok(new { message = genericMessage });

            await _passwordResetService.RequestAsync(
                PortalUserType.Client, user.Id, user.Email, user.Name, _portalUrl, "password_reset", tenant.CoreTenantId,
                tenant.LogoLightUrl, tenant.CommercialName);

            return Ok(new { message = genericMessage });
        }

        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] ClientResetPasswordRequest request)
        {
            // Se valida ANTES de consumir el token: si se consumiera primero y la contraseña no
            // cumple la política, el enlace de un solo uso quedaría inutilizado sin que la persona
            // haya logrado establecer nada — tendría que pedir uno nuevo por un simple typo.
            var passwordError = Fel.Core.Security.PasswordPolicy.Validate(request.NewPassword);
            if (passwordError != null) return BadRequest(passwordError);

            var consumed = await _passwordResetService.ConsumeAsync(request.Token, PortalUserType.Client);
            if (consumed == null) return BadRequest("El enlace no es válido o ya expiró. Solicita uno nuevo.");

            var user = await _dbContext.ClientUsers.FindAsync(consumed.Value.UserId);
            if (user == null) return BadRequest("El enlace no es válido o ya expiró. Solicita uno nuevo.");

            // La contraseña es de la persona (su correo), no de una empresa: se fija igual en todos sus accesos del tenant, y la invitación
            // queda cumplida en todos. Así quien lleva varios clientes recibe una sola invitación y tiene una sola clave.
            var tenantId = await _dbContext.Clients.Where(c => c.Id == user.ClientId).Select(c => c.TenantId).FirstAsync();
            var group = await _dbContext.ClientUsers.Where(u => u.Email == user.Email && u.Client.TenantId == tenantId).ToListAsync();
            var newHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
            foreach (var member in group)
            {
                member.PasswordHash = newHash;
                member.InvitedAt ??= DateTime.UtcNow;
                _accountSessions.RotateStamp(Fel.Core.Security.PortalKind.Client, member); // quien recupera su cuenta corta cualquier sesión abierta
            }
            await _dbContext.SaveChangesAsync();

            return Ok(new { message = "Contraseña actualizada correctamente." });
        }
    }

    public class ClientLoginRequest
    {
        public string TenantSlug { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        // Empresa a la que entra, cuando el correo tiene acceso a varias; nulo en el primer intento.
        public Guid? ClientId { get; set; }
    }

    public class ClientForgotPasswordRequest
    {
        public string TenantSlug { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
    }

    public class ClientResetPasswordRequest
    {
        public string Token { get; set; } = string.Empty;
        public string NewPassword { get; set; } = string.Empty;
    }
}
