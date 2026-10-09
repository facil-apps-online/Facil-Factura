using Fel.Core.Entities;
using Fel.Infrastructure.Data;
using Fel.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.Extensions.Configuration;

namespace Fel.Api.Superadmin.Controllers
{
    [ApiController]
    [Route("api/superadmin/auth")]
    [Microsoft.AspNetCore.Authorization.AllowAnonymous] // Login/setup/recuperación deben quedar accesibles sin token — el resto de la API exige [Authorize] por defecto (ver Program.cs).
    public class SuperadminAuthController : ControllerBase
    {
        private readonly FelDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly PasswordResetService _passwordResetService;
        private readonly Fel.Infrastructure.Security.AccountSessionService _accountSessions;
        private readonly string _portalUrl;

        public SuperadminAuthController(FelDbContext context, IConfiguration configuration, PasswordResetService passwordResetService, Fel.Infrastructure.Security.AccountSessionService accountSessions)
        {
            _context = context;
            _configuration = configuration;
            _passwordResetService = passwordResetService;
            _accountSessions = accountSessions;
            _portalUrl = configuration["PortalUrl"] ?? "https://admin.facil-factura.pro";
        }

        [HttpGet("status")]
        public async Task<IActionResult> GetStatus()
        {
            var exists = await _context.SuperadminUsers.AnyAsync();
            return Ok(new { setupRequired = !exists });
        }

        [HttpPost("setup")]
        public async Task<IActionResult> Setup([FromBody] SetupDto request)
        {
            var exists = await _context.SuperadminUsers.AnyAsync();
            if (exists)
            {
                return BadRequest("El Superadmin ya ha sido configurado. Por seguridad, no se pueden crear más cuentas maestras.");
            }

            var superadmin = new SuperadminUser
            {
                Id = Guid.NewGuid(),
                Email = request.Email,
                PasswordHash = Fel.Infrastructure.Security.SuperadminPasswordHasher.Hash(request.Password),
                CreatedAt = DateTime.UtcNow
            };

            _context.SuperadminUsers.Add(superadmin);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Superadmin configurado exitosamente" });
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto request)
        {
            var user = await _context.SuperadminUsers.FirstOrDefaultAsync(u => u.Email == request.Email);
            if (user == null)
            {
                return Unauthorized("Credenciales inválidas");
            }

            if (!Fel.Infrastructure.Security.SuperadminPasswordHasher.Verify(request.Password, user.PasswordHash))
            {
                return Unauthorized("Credenciales inválidas");
            }

            // Las contraseñas guardadas con el esquema anterior (SHA-256 con sal fija) se pasan a BCrypt la primera vez que entran bien.
            if (Fel.Infrastructure.Security.SuperadminPasswordHasher.IsLegacy(user.PasswordHash))
            {
                user.PasswordHash = Fel.Infrastructure.Security.SuperadminPasswordHasher.Hash(request.Password);
                await _context.SaveChangesAsync();
            }

            var session = _accountSessions.Issue(Fel.Core.Security.PortalKind.Superadmin, user, new[]
            {
                (ClaimTypes.NameIdentifier, user.Id.ToString()),
                (ClaimTypes.Email, user.Email),
                (ClaimTypes.Role, "Superadmin")
            });

            return Ok(new { token = session.Token, email = user.Email });
        }

        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] SuperadminForgotPasswordDto request)
        {
            const string genericMessage = "Si el correo está registrado, te enviamos un enlace para restablecer tu contraseña.";

            var user = await _context.SuperadminUsers.FirstOrDefaultAsync(u => u.Email == request.Email);
            if (user == null) return Ok(new { message = genericMessage });

            // Sin tenantCoreId: el personal Superadmin no pertenece a ningún tenant de Core.
            await _passwordResetService.RequestAsync(
                PortalUserType.Superadmin, user.Id, user.Email, user.Email, _portalUrl, "password_reset");

            return Ok(new { message = genericMessage });
        }

        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] SuperadminResetPasswordDto request)
        {
            var passwordError = Fel.Core.Security.PasswordPolicy.Validate(request.NewPassword);
            if (passwordError != null) return BadRequest(passwordError);

            var consumed = await _passwordResetService.ConsumeAsync(request.Token, PortalUserType.Superadmin);
            if (consumed == null) return BadRequest("El enlace no es válido o ya expiró. Solicita uno nuevo.");

            var user = await _context.SuperadminUsers.FindAsync(consumed.Value.UserId);
            if (user == null) return BadRequest("El enlace no es válido o ya expiró. Solicita uno nuevo.");

            user.PasswordHash = Fel.Infrastructure.Security.SuperadminPasswordHasher.Hash(request.NewPassword);
            _accountSessions.RotateStamp(Fel.Core.Security.PortalKind.Superadmin, user); // quien recupera su cuenta corta cualquier sesión abierta
            await _context.SaveChangesAsync();

            return Ok(new { message = "Contraseña actualizada correctamente." });
        }
    }

    public class SetupDto
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public class LoginDto
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public class SuperadminForgotPasswordDto
    {
        public string Email { get; set; } = string.Empty;
    }

    public class SuperadminResetPasswordDto
    {
        public string Token { get; set; } = string.Empty;
        public string NewPassword { get; set; } = string.Empty;
    }
}
