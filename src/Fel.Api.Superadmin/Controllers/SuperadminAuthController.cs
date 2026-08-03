using Fel.Core.Entities;
using Fel.Infrastructure.Data;
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
    public class SuperadminAuthController : ControllerBase
    {
        private readonly FelDbContext _context;
        private readonly IConfiguration _configuration;

        public SuperadminAuthController(FelDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
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
}
