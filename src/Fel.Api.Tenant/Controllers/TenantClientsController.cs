using System;
using System.Linq;
using System.Threading.Tasks;
using Fel.Core.Entities;
using Fel.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Fel.Api.Tenant.Controllers
{
    [ApiController]
    [Route("api/tenant/clients")]
    public class TenantClientsController : ControllerBase
    {
        private readonly FelDbContext _dbContext;

        public TenantClientsController(FelDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        // Simulación: en producción, esto vendría del JWT o Claims.
        private Guid GetCurrentTenantId()
        {
            if (Request.Headers.TryGetValue("x-tenant-id", out var tenantIdStr))
            {
                if (Guid.TryParse(tenantIdStr, out var tenantId))
                    return tenantId;
            }
            // Fallback (debería retornar 401, pero para efectos de prueba asumimos que viene en el header)
            throw new UnauthorizedAccessException("x-tenant-id Header is missing");
        }

        [HttpGet]
        public async Task<IActionResult> GetClients()
        {
            try
            {
                var tenantId = GetCurrentTenantId();
                var clients = await _dbContext.Clients
                    .Where(c => c.TenantId == tenantId)
                    .Select(c => new
                    {
                        c.Id,
                        c.CompanyName,
                        c.TaxId,
                        c.Email,
                        c.IsActive,
                        c.CreatedAt
                    })
                    .ToListAsync();

                return Ok(clients);
            }
            catch (Exception ex)
            {
                return Unauthorized(ex.Message);
            }
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetClient(Guid id)
        {
            var tenantId = GetCurrentTenantId();
            var client = await _dbContext.Clients
                .FirstOrDefaultAsync(c => c.Id == id && c.TenantId == tenantId);
                
            if (client == null) return NotFound();
            return Ok(new {
                client.Id,
                client.CompanyName,
                client.CommercialName,
                client.TaxId,
                client.VerificationDigit,
                client.Email,
                client.Phone,
                client.Address,
                client.City,
                client.TaxRegime,
                client.EconomicActivity,
                client.Latitude,
                client.Longitude,
                client.IsActive,
                client.LiveApiKey,
                client.LiveApiSecret,
                client.TestApiKey,
                client.TestApiSecret
            });
        }

        [HttpPost]
        public async Task<IActionResult> CreateClient([FromBody] CreateClientRequest request)
        {
            var tenantId = GetCurrentTenantId();

            if (await _dbContext.Clients.AnyAsync(c => c.TaxId == request.TaxId && c.TenantId == tenantId))
            {
                return BadRequest("El NIT ya está registrado en este Tenant.");
            }

            var client = new Client
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                CompanyName = request.CompanyName,
                CommercialName = request.CommercialName,
                TaxId = request.TaxId,
                VerificationDigit = request.VerificationDigit,
                Email = request.Email,
                Phone = request.Phone,
                Address = request.Address,
                City = request.City,
                TaxRegime = request.TaxRegime,
                EconomicActivity = request.EconomicActivity,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            _dbContext.Clients.Add(client);
            await _dbContext.SaveChangesAsync();

            return Ok(new { client.Id, client.CompanyName, client.TaxId, client.Email, client.IsActive });
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateClient(Guid id, [FromBody] UpdateClientRequest request)
        {
            var tenantId = GetCurrentTenantId();
            var client = await _dbContext.Clients.FirstOrDefaultAsync(c => c.Id == id && c.TenantId == tenantId);
            
            if (client == null) return NotFound();

            client.CompanyName = request.CompanyName;
            client.CommercialName = request.CommercialName;
            client.Email = request.Email;
            client.Phone = request.Phone;
            client.TaxId = request.TaxId;
            client.VerificationDigit = request.VerificationDigit;
            client.Address = request.Address;
            client.City = request.City;
            client.TaxRegime = request.TaxRegime;
            client.EconomicActivity = request.EconomicActivity;
            client.Latitude = request.Latitude;
            client.Longitude = request.Longitude;

            await _dbContext.SaveChangesAsync();

            return Ok(new { client.Id, client.CompanyName, client.TaxId, client.Email, client.IsActive });
        }
        
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteClient(Guid id)
        {
            var tenantId = GetCurrentTenantId();
            var client = await _dbContext.Clients.FirstOrDefaultAsync(c => c.Id == id && c.TenantId == tenantId);
            
            if (client == null) return NotFound();

            // Lógica de Soft Delete
            client.IsActive = false;
            await _dbContext.SaveChangesAsync();

            return NoContent();
        }

        [HttpPost("{id}/generate-key")]
        public async Task<IActionResult> GenerateApiKey(Guid id, [FromQuery] string env)
        {
            var tenantId = GetCurrentTenantId();
            var client = await _dbContext.Clients.FirstOrDefaultAsync(c => c.Id == id && c.TenantId == tenantId);
            
            if (client == null) return NotFound();

            var newKey = $"sk_{env.ToLower()}_{Guid.NewGuid().ToString("N")}";
            var newSecret = Guid.NewGuid().ToString("N");

            if (env.Equals("live", StringComparison.OrdinalIgnoreCase))
            {
                client.LiveApiKey = newKey;
                client.LiveApiSecret = newSecret;
            }
            else if (env.Equals("test", StringComparison.OrdinalIgnoreCase))
            {
                client.TestApiKey = newKey;
                client.TestApiSecret = newSecret;
            }
            else
            {
                return BadRequest("Invalid environment. Use 'live' or 'test'.");
            }

            await _dbContext.SaveChangesAsync();

            return Ok(new { key = newKey, secret = newSecret });
        }
    }

    public class CreateClientRequest
    {
        public string CompanyName { get; set; } = string.Empty;
        public string CommercialName { get; set; } = string.Empty;
        public string TaxId { get; set; } = string.Empty;
        public string VerificationDigit { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string TaxRegime { get; set; } = string.Empty;
        public string EconomicActivity { get; set; } = string.Empty;
    }

    public class UpdateClientRequest
    {
        public string CompanyName { get; set; } = string.Empty;
        public string CommercialName { get; set; } = string.Empty;
        public string TaxId { get; set; } = string.Empty;
        public string VerificationDigit { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string TaxRegime { get; set; } = string.Empty;
        public string EconomicActivity { get; set; } = string.Empty;
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
    }
}
