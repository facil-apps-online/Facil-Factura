using System;
using System.Linq;
using System.Threading.Tasks;
using Fel.Core.Entities;
using Fel.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Fel.Api.Controllers
{
    [ApiController]
    [Route("api/superadmin/tenants")]
    public class SuperadminTenantsController : ControllerBase
    {
        private readonly FelDbContext _dbContext;

        public SuperadminTenantsController(FelDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        [HttpGet]
        public async Task<IActionResult> GetTenants()
        {
            var tenants = await _dbContext.Tenants
                .Select(t => new
                {
                    t.Id,
                    t.Name,
                    t.CommercialName,
                    t.Slug,
                    t.IsActive,
                    t.CreatedAt
                })
                .ToListAsync();

            return Ok(tenants);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetTenant(Guid id)
        {
            var tenant = await _dbContext.Tenants.FindAsync(id);
            if (tenant == null) return NotFound();
            return Ok(tenant);
        }

        [HttpPost]
        public async Task<IActionResult> CreateTenant([FromBody] CreateTenantRequest request)
        {
            if (await _dbContext.Tenants.AnyAsync(t => t.Slug == request.Slug))
            {
                return BadRequest("El slug ya está en uso.");
            }

            var tenant = new Tenant
            {
                Id = Guid.NewGuid(),
                Name = request.Name,
                CommercialName = request.CommercialName,
                Email = request.Email,
                Slug = request.Slug,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            _dbContext.Tenants.Add(tenant);
            await _dbContext.SaveChangesAsync();

            return Ok(tenant);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateTenant(Guid id, [FromBody] UpdateTenantRequest request)
        {
            var tenant = await _dbContext.Tenants.FindAsync(id);
            if (tenant == null) return NotFound();

            tenant.Name = request.Name;
            tenant.CommercialName = request.CommercialName;
            tenant.Email = request.Email;
            
            // Fiscal Info
            tenant.TaxId = request.TaxId;
            tenant.VerificationDigit = request.VerificationDigit;
            tenant.Address = request.Address;
            tenant.City = request.City;
            tenant.Phone = request.Phone;
            tenant.TaxRegime = request.TaxRegime;
            tenant.EconomicActivity = request.EconomicActivity;
            tenant.Latitude = request.Latitude;
            tenant.Longitude = request.Longitude;

            await _dbContext.SaveChangesAsync();

            return Ok(tenant);
        }

        [HttpGet("{id}/users")]
        public async Task<IActionResult> GetTenantUsers(Guid id)
        {
            var users = await _dbContext.TenantUsers
                .Where(u => u.TenantId == id)
                .Select(u => new { u.Id, u.Name, u.Email, u.IsActive, u.CreatedAt })
                .ToListAsync();

            return Ok(users);
        }

        [HttpPost("{id}/users")]
        public async Task<IActionResult> CreateTenantUser(Guid id, [FromBody] CreateTenantUserRequest request)
        {
            if (!await _dbContext.Tenants.AnyAsync(t => t.Id == id)) return NotFound("Tenant no existe.");
            if (await _dbContext.TenantUsers.AnyAsync(u => u.Email == request.Email)) return BadRequest("Email ya registrado.");

            // Hashing the password using BCrypt (assuming it's used or simple fallback)
            string passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);

            var user = new TenantUser
            {
                Id = Guid.NewGuid(),
                TenantId = id,
                Name = request.Name,
                Email = request.Email,
                PasswordHash = passwordHash,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            _dbContext.TenantUsers.Add(user);
            await _dbContext.SaveChangesAsync();

            return Ok(new { user.Id, user.Name, user.Email, user.IsActive });
        }
    }

    public class CreateTenantUserRequest
    {
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public class CreateTenantRequest
    {
        public string Name { get; set; } = string.Empty;
        public string CommercialName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
    }

    public class UpdateTenantRequest
    {
        public string Name { get; set; } = string.Empty;
        public string CommercialName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        
        public string TaxId { get; set; } = string.Empty;
        public string VerificationDigit { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string TaxRegime { get; set; } = string.Empty;
        public string EconomicActivity { get; set; } = string.Empty;
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
    }
}
