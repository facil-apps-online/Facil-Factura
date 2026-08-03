using System;
using System.Threading.Tasks;
using Fel.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Fel.Api.Tenant.Controllers
{
    [ApiController]
    [Route("api/tenant/auth")]
    public class TenantAuthController : ControllerBase
    {
        private readonly FelDbContext _dbContext;

        public TenantAuthController(FelDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] TenantLoginRequest request)
        {
            var user = await _dbContext.TenantUsers
                .Include(u => u.Tenant)
                .FirstOrDefaultAsync(u => u.Email == request.Email);

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

            if (!user.Tenant.IsActive)
            {
                return Unauthorized("La cuenta principal (Tenant) está suspendida.");
            }

            // Retornamos el TenantId como token de sesión para el header x-tenant-id
            return Ok(new
            {
                token = user.TenantId.ToString(),
                tenantId = user.TenantId,
                name = user.Name,
                email = user.Email,
                commercialName = user.Tenant.CommercialName
            });
        }
    }

    public class TenantLoginRequest
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }
}
