using System;
using System.Linq;
using System.Threading.Tasks;
using Fel.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Fel.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ClientController : ControllerBase
    {
        private readonly FelDbContext _dbContext;

        public ClientController(FelDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        [HttpPost("{clientId}/apikey")]
        public async Task<IActionResult> GenerateApiKey(Guid clientId)
        {
            var client = await _dbContext.Clients.FindAsync(clientId);
            if (client == null)
            {
                return NotFound(new { Message = "Client not found" });
            }

            // Generate a secure API Key (simplified for example)
            string newApiKey = $"FEL-{Guid.NewGuid():N}";
            
            client.SoftwarePin = newApiKey; // Storing it in SoftwarePin or dedicated field
            
            await _dbContext.SaveChangesAsync();

            return Ok(new { ApiKey = newApiKey });
        }

        [HttpPost("{clientId}/testset")]
        public async Task<IActionResult> ConfigureTestSet(Guid clientId, [FromBody] TestSetRequest request)
        {
            var client = await _dbContext.Clients.FindAsync(clientId);
            if (client == null)
            {
                return NotFound(new { Message = "Client not found" });
            }

            client.SoftwareId = request.TestSetId;
            await _dbContext.SaveChangesAsync();

            return Ok(new { Message = "Test Set ID Configured", TestSetId = client.SoftwareId });
        }
    }

    public class TestSetRequest
    {
        public string TestSetId { get; set; } = string.Empty;
    }
}
