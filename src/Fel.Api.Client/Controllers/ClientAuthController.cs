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

        public ClientAuthController(FelDbContext dbContext, PasswordResetService passwordResetService, Fel.Infrastructure.Security.AccountSessionService accountSessions, IConfiguration config)
        {
            _dbContext = dbContext;
            _passwordResetService = passwordResetService;
            _accountSessions = accountSessions;
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

            var user = await _dbContext.ClientUsers
                .Include(u => u.Client)
                .FirstOrDefaultAsync(u => u.Email == request.Email && u.Client.TenantId == tenant.Id);

            if (user == null || !user.IsActive || !user.Client.IsActive)
            {
                return Unauthorized("Credenciales incorrectas o usuario inactivo.");
            }

            bool isPasswordValid = BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash);
            if (!isPasswordValid)
            {
                return Unauthorized("Credenciales incorrectas.");
            }

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
            {
                return Unauthorized("No tienes sucursales activas asignadas. Contacta a tu administrador.");
            }

            var token = _accountSessions.Issue(Fel.Core.Security.PortalKind.Client, user, new[]
            {
                ("ClientId", user.ClientId.ToString()),
                (System.Security.Claims.ClaimTypes.NameIdentifier, user.Id.ToString()),
                (System.Security.Claims.ClaimTypes.Email, user.Email)
            }).Token;

            return Ok(new
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

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
            _accountSessions.RotateStamp(Fel.Core.Security.PortalKind.Client, user); // quien recupera su cuenta corta cualquier sesión abierta
            await _dbContext.SaveChangesAsync();

            return Ok(new { message = "Contraseña actualizada correctamente." });
        }
    }

    public class ClientLoginRequest
    {
        public string TenantSlug { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
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
