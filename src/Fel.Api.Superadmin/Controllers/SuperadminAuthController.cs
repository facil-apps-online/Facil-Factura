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
        private readonly string _portalUrl;

        public SuperadminAuthController(FelDbContext context, IConfiguration configuration, PasswordResetService passwordResetService)
        {
            _context = context;
            _configuration = configuration;
            _passwordResetService = passwordResetService;
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
                PasswordHash = HashPassword(request.Password),
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

            var hash = HashPassword(request.Password);
            if (user.PasswordHash != hash)
            {
                return Unauthorized("Credenciales inválidas");
            }

            var tokenHandler = new JwtSecurityTokenHandler();
            // Use MasterKey from appsettings.json for JWT signing, ensure it's at least 32 bytes
            var keyStr = _configuration.GetValue<string>("MasterKey") ?? "SUPER_SECRET_FALLBACK_KEY_MUST_BE_32_CHARS_LONG_OR_MORE_123456";
            var key = Encoding.UTF8.GetBytes(keyStr.PadRight(32, '0')); 
            
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new[] 
                {
                    new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                    new Claim(ClaimTypes.Email, user.Email),
                    new Claim(ClaimTypes.Role, "Superadmin")
                }),
                Expires = DateTime.UtcNow.AddHours(24),
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };
            
            var token = tokenHandler.CreateToken(tokenDescriptor);
            var jwtToken = tokenHandler.WriteToken(token);

            return Ok(new { token = jwtToken, email = user.Email });
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
            var consumed = await _passwordResetService.ConsumeAsync(request.Token, PortalUserType.Superadmin);
            if (consumed == null) return BadRequest("El enlace no es válido o ya expiró. Solicita uno nuevo.");

            var user = await _context.SuperadminUsers.FindAsync(consumed.Value.UserId);
            if (user == null) return BadRequest("El enlace no es válido o ya expiró. Solicita uno nuevo.");

            user.PasswordHash = HashPassword(request.NewPassword);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Contraseña actualizada correctamente." });
        }

        private string HashPassword(string password)
        {
            using (var sha256 = SHA256.Create())
            {
                var bytes = Encoding.UTF8.GetBytes(password + "FEL_SALT_SECURE");
                var hash = sha256.ComputeHash(bytes);
                return Convert.ToBase64String(hash);
            }
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
