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

namespace Fel.Api.Tenant.Controllers
{
    [ApiController]
    [Route("api/tenant/auth")]
    public class TenantAuthController : ControllerBase
    {
        private readonly FelDbContext _dbContext;
        private readonly PasswordResetService _passwordResetService;
        private readonly Fel.Infrastructure.Security.AccountSessionService _accountSessions;
        private readonly string _portalUrl;

        public TenantAuthController(FelDbContext dbContext, PasswordResetService passwordResetService, Fel.Infrastructure.Security.AccountSessionService accountSessions, IConfiguration config)
        {
            _dbContext = dbContext;
            _passwordResetService = passwordResetService;
            _accountSessions = accountSessions;
            _portalUrl = config["PortalUrl"] ?? "https://tenants.facil-factura.pro";
        }

        [AllowAnonymous]
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] TenantLoginRequest request)
        {
            var user = await _dbContext.TenantUsers.FirstOrDefaultAsync(u => u.Email == request.Email);

            if (user == null || !user.IsActive)
            {
                return Unauthorized("Credenciales incorrectas o usuario inactivo.");
            }

            // Verificar password con BCrypt
            bool isPasswordValid = BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash);

            if (!isPasswordValid)
            {
                return Unauthorized("Credenciales incorrectas.");
            }

            // El acceso ya no es "un usuario = un tenant": la identidad puede tener asignaciones a
            // varios. Solo cuentan las vigentes (asignación activa y tenant activo) — una
            // asignación revocada o un tenant suspendido no deben dejar entrar a esa cuenta.
            var tenantsAccesibles = await _dbContext.TenantUserAssignments
                .Where(a => a.TenantUserId == user.Id && a.IsActive && a.Tenant.IsActive)
                .Select(a => new { a.TenantId, a.Tenant.CommercialName, a.Tenant.Slug })
                .ToListAsync();

            if (tenantsAccesibles.Count == 0)
            {
                return Unauthorized("Esta cuenta no tiene acceso vigente a ningún tenant.");
            }

            // Tenant con el que abre la sesión: el que tenía como TenantId antes de que existieran
            // las asignaciones múltiples, si sigue vigente; si no, el primero que tenga.
            var tenantInicial = tenantsAccesibles.FirstOrDefault(t => t.TenantId == user.TenantId)
                                 ?? tenantsAccesibles[0];

            var token = _accountSessions.Issue(Fel.Core.Security.PortalKind.Tenant, user, new[]
            {
                ("TenantId", tenantInicial.TenantId.ToString()),
                (System.Security.Claims.ClaimTypes.NameIdentifier, user.Id.ToString()),
                (System.Security.Claims.ClaimTypes.Email, user.Email)
            }).Token;

            return Ok(new
            {
                token,
                tenantId = tenantInicial.TenantId,
                name = user.Name,
                email = user.Email,
                commercialName = tenantInicial.CommercialName,
                // Solo va poblado cuando hay más de uno: así el portal no necesita cambiar nada
                // para las cuentas de siempre, con un único tenant.
                tenants = tenantsAccesibles.Count > 1
                    ? tenantsAccesibles.Select(t => new { id = t.TenantId, commercialName = t.CommercialName, slug = t.Slug })
                    : null
            });
        }

        /// <summary>
        /// Reemite el token para otro tenant al que la cuenta autenticada tenga acceso vigente,
        /// sin tener que cerrar sesión y volver a poner la contraseña.
        /// </summary>
        [Authorize]
        [HttpPost("switch-tenant")]
        public async Task<IActionResult> SwitchTenant([FromBody] SwitchTenantRequest request)
        {
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdClaim, out var userId))
            {
                return Unauthorized();
            }

            var user = await _dbContext.TenantUsers.FirstOrDefaultAsync(u => u.Id == userId && u.IsActive);
            if (user == null) return Unauthorized();

            // Se revalida contra la base en cada cambio, no contra la lista que trajo el login: si
            // a la cuenta le revocaron el acceso a ese tenant en el ínterin, no debe poder entrar
            // solo porque el token viejo seguía siendo válido.
            var asignacion = await _dbContext.TenantUserAssignments
                .Include(a => a.Tenant)
                .FirstOrDefaultAsync(a => a.TenantUserId == userId && a.TenantId == request.TenantId
                                        && a.IsActive && a.Tenant.IsActive);

            if (asignacion == null)
            {
                return Unauthorized("No tienes acceso vigente a ese tenant.");
            }

            var token = _accountSessions.Issue(Fel.Core.Security.PortalKind.Tenant, user, new[]
            {
                ("TenantId", asignacion.TenantId.ToString()),
                (System.Security.Claims.ClaimTypes.NameIdentifier, user.Id.ToString()),
                (System.Security.Claims.ClaimTypes.Email, user.Email)
            }, Fel.Infrastructure.Security.AccountSessionService.SessionStart(User)).Token;

            return Ok(new
            {
                token,
                tenantId = asignacion.TenantId,
                name = user.Name,
                email = user.Email,
                commercialName = asignacion.Tenant.CommercialName
            });
        }
        [AllowAnonymous]
        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] TenantForgotPasswordRequest request)
        {
            const string genericMessage = "Si el correo está registrado, te enviamos un enlace para restablecer tu contraseña.";

            var user = await _dbContext.TenantUsers
                .Include(u => u.Tenant)
                .FirstOrDefaultAsync(u => u.Email == request.Email && u.IsActive);
            if (user == null) return Ok(new { message = genericMessage });

            await _passwordResetService.RequestAsync(
                PortalUserType.Tenant, user.Id, user.Email, user.Name, _portalUrl, "password_reset", user.Tenant.CoreTenantId,
                user.Tenant.LogoLightUrl, user.Tenant.CommercialName);

            return Ok(new { message = genericMessage });
        }

        [AllowAnonymous]
        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] TenantResetPasswordRequest request)
        {
            var passwordError = Fel.Core.Security.PasswordPolicy.Validate(request.NewPassword);
            if (passwordError != null) return BadRequest(passwordError);

            var consumed = await _passwordResetService.ConsumeAsync(request.Token, PortalUserType.Tenant);
            if (consumed == null) return BadRequest("El enlace no es válido o ya expiró. Solicita uno nuevo.");

            var user = await _dbContext.TenantUsers.FindAsync(consumed.Value.UserId);
            if (user == null) return BadRequest("El enlace no es válido o ya expiró. Solicita uno nuevo.");

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
            _accountSessions.RotateStamp(Fel.Core.Security.PortalKind.Tenant, user); // quien recupera su cuenta corta cualquier sesión abierta
            await _dbContext.SaveChangesAsync();

            return Ok(new { message = "Contraseña actualizada correctamente." });
        }
    }

    public class TenantLoginRequest
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public class SwitchTenantRequest
    {
        public Guid TenantId { get; set; }
    }

    public class TenantForgotPasswordRequest
    {
        public string Email { get; set; } = string.Empty;
    }

    public class TenantResetPasswordRequest
    {
        public string Token { get; set; } = string.Empty;
        public string NewPassword { get; set; } = string.Empty;
    }
}
