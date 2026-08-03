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
    [Route("api/client/products")]
    public class ProductController : ControllerBase
    {
        private readonly FelDbContext _dbContext;

        public ProductController(FelDbContext dbContext)
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
                var products = await _dbContext.Products
                    .Where(p => p.ClientId == clientId)
                    .OrderByDescending(p => p.CreatedAt)
                    .ToListAsync();

                return Ok(products);
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
                var product = await _dbContext.Products
                    .FirstOrDefaultAsync(p => p.Id == id && p.ClientId == clientId);

                if (product == null) return NotFound("Producto no encontrado.");
                return Ok(product);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(ex.Message);
            }
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] Product product)
        {
            try
            {
                var clientId = GetCurrentClientId();
                
                // Validar si ya existe
                var existing = await _dbContext.Products
                    .FirstOrDefaultAsync(p => p.ClientId == clientId && p.Code == product.Code);
                    
                if (existing != null)
                    return BadRequest("Ya existe un producto con este código/SKU.");

                product.Id = Guid.NewGuid();
                product.ClientId = clientId;
                product.CreatedAt = DateTime.UtcNow;
                product.UpdatedAt = DateTime.UtcNow;

                _dbContext.Products.Add(product);
                await _dbContext.SaveChangesAsync();

                return CreatedAtAction(nameof(GetById), new { id = product.Id }, product);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(ex.Message);
            }
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] Product updateData)
        {
            try
            {
                var clientId = GetCurrentClientId();
                var product = await _dbContext.Products
                    .FirstOrDefaultAsync(p => p.Id == id && p.ClientId == clientId);

                if (product == null) return NotFound("Producto no encontrado.");

                product.Code = updateData.Code;
                product.StandardCode = updateData.StandardCode;
                product.Name = updateData.Name;
                product.UnitPrice = updateData.UnitPrice;
                product.TaxRate = updateData.TaxRate;
                product.UnitOfMeasure = updateData.UnitOfMeasure;
                product.UpdatedAt = DateTime.UtcNow;

                await _dbContext.SaveChangesAsync();
                return Ok(product);
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
                var product = await _dbContext.Products
                    .FirstOrDefaultAsync(p => p.Id == id && p.ClientId == clientId);

                if (product == null) return NotFound("Producto no encontrado.");

                _dbContext.Products.Remove(product);
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
