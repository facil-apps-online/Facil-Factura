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
    [Route("api/tenant/clients/{clientId}/resolutions")]
    public class TenantResolutionsController : ControllerBase
    {
        private readonly FelDbContext _dbContext;

        public TenantResolutionsController(FelDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        private Guid GetCurrentTenantId()
        {
            if (Request.Headers.TryGetValue("x-tenant-id", out var tenantIdStr))
            {
                if (Guid.TryParse(tenantIdStr, out var tenantId))
                    return tenantId;
            }
            throw new UnauthorizedAccessException("x-tenant-id Header is missing");
        }

        [HttpGet]
        public async Task<IActionResult> GetResolutions(Guid clientId)
        {
            var tenantId = GetCurrentTenantId();
            
            var clientExists = await _dbContext.Clients.AnyAsync(c => c.Id == clientId && c.TenantId == tenantId);
            if (!clientExists) return Forbid();

            var resolutions = await _dbContext.Set<Resolution>()
                .Where(r => r.ClientId == clientId && r.IsActive)
                .OrderByDescending(r => r.ValidFrom)
                .Select(r => new {
                    r.Id,
                    r.ResolutionNumber,
                    r.Prefix,
                    r.NumberStart,
                    r.NumberEnd,
                    r.ValidFrom,
                    r.ValidTo,
                    r.TechnicalKey,
                    r.DocumentType
                })
                .ToListAsync();

            return Ok(resolutions);
        }

        [HttpPost]
        public async Task<IActionResult> CreateResolution(Guid clientId, [FromBody] CreateResolutionRequest request)
        {
            var tenantId = GetCurrentTenantId();
            var clientExists = await _dbContext.Clients.AnyAsync(c => c.Id == clientId && c.TenantId == tenantId);
            if (!clientExists) return Forbid();

            var resolution = new Resolution
            {
                Id = Guid.NewGuid(),
                ClientId = clientId,
                ResolutionNumber = request.ResolutionNumber,
                Prefix = request.Prefix ?? "",
                NumberStart = request.NumberStart,
                NumberEnd = request.NumberEnd,
                ValidFrom = request.ValidFrom,
                ValidTo = request.ValidTo,
                TechnicalKey = request.TechnicalKey ?? "",
                DocumentType = request.DocumentType,
                IsActive = true
            };

            _dbContext.Set<Resolution>().Add(resolution);
            await _dbContext.SaveChangesAsync();

            return Ok(new {
                resolution.Id,
                resolution.ResolutionNumber,
                resolution.Prefix,
                resolution.NumberStart,
                resolution.NumberEnd,
                resolution.ValidFrom,
                resolution.ValidTo,
                resolution.DocumentType
            });
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteResolution(Guid clientId, Guid id)
        {
            var tenantId = GetCurrentTenantId();
            var clientExists = await _dbContext.Clients.AnyAsync(c => c.Id == clientId && c.TenantId == tenantId);
            if (!clientExists) return Forbid();

            var resolution = await _dbContext.Set<Resolution>().FirstOrDefaultAsync(r => r.Id == id && r.ClientId == clientId);
            if (resolution == null) return NotFound();

            resolution.IsActive = false;
            await _dbContext.SaveChangesAsync();

            return NoContent();
        }
    }

    public class CreateResolutionRequest
    {
        public string ResolutionNumber { get; set; } = string.Empty;
        public string Prefix { get; set; } = string.Empty;
        public long NumberStart { get; set; }
        public long NumberEnd { get; set; }
        public DateTime ValidFrom { get; set; }
        public DateTime ValidTo { get; set; }
        public string TechnicalKey { get; set; } = string.Empty;
        public string DocumentType { get; set; } = string.Empty;
    }
}
