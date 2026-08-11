using System;
using System.Threading.Tasks;
using Fel.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Fel.Api.Client.Controllers
{
    [ApiController]
    [Route("api/v1/branding")]
    public class ClientBrandingController : ControllerBase
    {
        private readonly FelDbContext _dbContext;

        public ClientBrandingController(FelDbContext dbContext)
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

        [HttpGet("my-branding")]
        public async Task<IActionResult> GetMyBranding()
        {
            try
            {
                var clientId = GetCurrentClientId();

                var client = await _dbContext.Clients
                    .Include(c => c.Tenant)
                    .FirstOrDefaultAsync(c => c.Id == clientId);

                if (client == null)
                    return NotFound(new { Message = "Client not found" });

                // Branding efectivo: lo del cliente, con fallback al tenant que lo provee
                var branding = new
                {
                    CompanyName = client.CommercialName,
                    LogoLightUrl = !string.IsNullOrWhiteSpace(client.LogoLightUrl) ? client.LogoLightUrl : client.Tenant.LogoLightUrl,
                    LogoDarkUrl = !string.IsNullOrWhiteSpace(client.LogoDarkUrl) ? client.LogoDarkUrl : client.Tenant.LogoDarkUrl,
                    PrimaryColorLight = !string.IsNullOrWhiteSpace(client.PrimaryColorLight) ? client.PrimaryColorLight : client.Tenant.PrimaryColorLight,
                    PrimaryColorDark = !string.IsNullOrWhiteSpace(client.PrimaryColorDark) ? client.PrimaryColorDark : client.Tenant.PrimaryColorDark,
                    HasCustomLogo = !string.IsNullOrWhiteSpace(client.LogoLightUrl)
                };

                return Ok(branding);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(ex.Message);
            }
        }

        [HttpPut("my-branding")]
        public async Task<IActionResult> UpdateMyBranding([FromBody] UpdateClientBrandingRequest request)
        {
            try
            {
                var clientId = GetCurrentClientId();

                var client = await _dbContext.Clients.FirstOrDefaultAsync(c => c.Id == clientId);
                if (client == null)
                    return NotFound(new { Message = "Client not found" });

                client.LogoLightUrl = request.LogoLightUrl ?? client.LogoLightUrl;
                client.LogoDarkUrl = request.LogoDarkUrl ?? client.LogoDarkUrl;
                client.PrimaryColorLight = request.PrimaryColorLight ?? client.PrimaryColorLight;
                client.PrimaryColorDark = request.PrimaryColorDark ?? client.PrimaryColorDark;

                await _dbContext.SaveChangesAsync();
                return Ok(new { Message = "Branding actualizado correctamente." });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(ex.Message);
            }
        }
    }

    public class UpdateClientBrandingRequest
    {
        public string? LogoLightUrl { get; set; }
        public string? LogoDarkUrl { get; set; }
        public string? PrimaryColorLight { get; set; }
        public string? PrimaryColorDark { get; set; }
    }
}
