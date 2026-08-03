using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Fel.Core.Entities;
using Fel.Infrastructure.Data;

namespace Fel.Api.Client.Controllers
{
    [ApiController]
    [Route("api/client/customers")]
    public class CustomerController : ControllerBase
    {
        private readonly FelDbContext _dbContext;

        public CustomerController(FelDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        private Guid GetCurrentClientId()
        {
            if (Request.Headers.TryGetValue("x-client-id", out var clientIdStr))
            {
                if (Guid.TryParse(clientIdStr, out var clientId))
                    return clientId;
            }
            throw new UnauthorizedAccessException("x-client-id Header is missing");
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            try
            {
                var clientId = GetCurrentClientId();
                var customers = await _dbContext.Customers
                    .Where(c => c.ClientId == clientId)
                    .OrderByDescending(c => c.CreatedAt)
                    .ToListAsync();

                return Ok(customers);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(ex.Message);
            }
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            try
            {
                var clientId = GetCurrentClientId();
                var customer = await _dbContext.Customers
                    .FirstOrDefaultAsync(c => c.Id == id && c.ClientId == clientId);

                if (customer == null) return NotFound("Cliente no encontrado.");
                return Ok(customer);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(ex.Message);
            }
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] Customer customer)
        {
            try
            {
                var clientId = GetCurrentClientId();
                
                // Validar si ya existe
                var existing = await _dbContext.Customers
                    .FirstOrDefaultAsync(c => c.ClientId == clientId && c.IdentificationNumber == customer.IdentificationNumber);
                    
                if (existing != null)
                    return BadRequest("Ya existe un cliente con este número de identificación.");

                customer.Id = Guid.NewGuid();
                customer.ClientId = clientId;
                customer.CreatedAt = DateTime.UtcNow;
                customer.UpdatedAt = DateTime.UtcNow;

                _dbContext.Customers.Add(customer);
                await _dbContext.SaveChangesAsync();

                return CreatedAtAction(nameof(GetById), new { id = customer.Id }, customer);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(ex.Message);
            }
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] Customer updateData)
        {
            try
            {
                var clientId = GetCurrentClientId();
                var customer = await _dbContext.Customers
                    .FirstOrDefaultAsync(c => c.Id == id && c.ClientId == clientId);

                if (customer == null) return NotFound("Cliente no encontrado.");

                customer.Name = updateData.Name;
                customer.IdentificationType = updateData.IdentificationType;
                customer.IdentificationNumber = updateData.IdentificationNumber;
                customer.VerificationDigit = updateData.VerificationDigit;
                customer.Email = updateData.Email;
                customer.Phone = updateData.Phone;
                customer.Address = updateData.Address;
                customer.CityCode = updateData.CityCode;
                customer.CityName = updateData.CityName;
                customer.TaxRegime = updateData.TaxRegime;
                customer.FiscalResponsibilities = updateData.FiscalResponsibilities;
                customer.UpdatedAt = DateTime.UtcNow;

                await _dbContext.SaveChangesAsync();
                return Ok(customer);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(ex.Message);
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            try
            {
                var clientId = GetCurrentClientId();
                var customer = await _dbContext.Customers
                    .FirstOrDefaultAsync(c => c.Id == id && c.ClientId == clientId);

                if (customer == null) return NotFound("Cliente no encontrado.");

                _dbContext.Customers.Remove(customer);
                await _dbContext.SaveChangesAsync();
                return NoContent();
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(ex.Message);
            }
        }
    }
}
