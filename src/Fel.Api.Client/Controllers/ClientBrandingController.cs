using System;
using System.Threading.Tasks;
using Fel.Core.Interfaces;
using Fel.Core.Models;
using Fel.Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Fel.Api.Client.Controllers
{
    [ApiController]
    [Route("api/v1/branding")]
    public class ClientBrandingController : ControllerBase
    {
        private const string ApiBaseUrl = "https://api.facil-factura.pro";
        private readonly FelDbContext _dbContext;
        private readonly IPublicFileStorageService _fileStorage;

        public ClientBrandingController(FelDbContext dbContext, IPublicFileStorageService fileStorage)
        {
            _dbContext = dbContext;
            _fileStorage = fileStorage;
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

                // La interfaz del portal de clientes es marca blanca del Tenant, no del cliente: el
                // cliente no tiene esquema de colores propio ni logo de portal — logo/nombre/color
                // de acá vienen siempre del Tenant, sin fallback al cliente (antes sí se le daba
                // prioridad al logo/color del cliente si los había cargado, lo cual pisaba la marca
                // del Tenant en su propio portal blanco). El logo que el cliente carga en "Tu
                // Identidad Visual" sigue existiendo, pero solo para InvoiceLogoUrl (la factura),
                // nunca para esta interfaz.
                var branding = new
                {
                    CompanyName = client.Tenant.CommercialName,
                    LogoLightUrl = client.Tenant.LogoLightUrl,
                    LogoDarkUrl = client.Tenant.LogoDarkUrl,
                    PrimaryColorLight = client.Tenant.PrimaryColorLight,
                    PrimaryColorDark = client.Tenant.PrimaryColorDark,
                    InvoiceLogoUrl = client.LogoLightUrl,
                    HasCustomLogo = !string.IsNullOrWhiteSpace(client.LogoLightUrl),
                    // Para el header del portal: identifica al Client (no al Tenant, que ya
                    // aparece en el sidebar) cuando no tiene logo propio cargado.
                    ClientName = string.IsNullOrWhiteSpace(client.CommercialName) ? client.CompanyName : client.CommercialName,
                    UnitOfMeasureDisplayOverride = client.UnitOfMeasureDisplayOverride,
                    // Formato numérico del portal y de los PDF de este cliente: "." (punto decimal) o ",".
                    DecimalSeparator = client.DecimalSeparator
                };

                return Ok(branding);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(ex.Message);
            }
        }

        [HttpPost("logo")]
        public async Task<IActionResult> UploadLogo(IFormFile file)
        {
            try
            {
                var clientId = GetCurrentClientId();

                if (file == null || file.Length == 0)
                {
                    return BadRequest("Debes adjuntar un archivo de imagen.");
                }

                if (file.Length > 5 * 1024 * 1024)
                {
                    return BadRequest("La imagen no puede superar 5MB.");
                }

                var client = await _dbContext.Clients.FirstOrDefaultAsync(c => c.Id == clientId);
                if (client == null) return NotFound(new { Message = "Client not found" });

                using var stream = file.OpenReadStream();
                var relativeKey = await _fileStorage.SaveFileAsync("logos", clientId, stream, file.FileName);
                client.LogoLightUrl = $"{ApiBaseUrl}/api/client/files/{relativeKey}";
                await _dbContext.SaveChangesAsync();

                return Ok(new { logoLightUrl = client.LogoLightUrl });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(ex.Message);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // El cliente ya no tiene esquema de colores propio (ver comentario en GetMyBranding) — este
        // endpoint solo actualiza el logo que usan sus facturas, no hay campo de color que aceptar.
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
                // "" = usar el default del catálogo (limpia el override); con valor, lo fuerza.
                client.UnitOfMeasureDisplayOverride = string.IsNullOrEmpty(request.UnitOfMeasureDisplayOverride)
                    ? null
                    : request.UnitOfMeasureDisplayOverride;

                // Solo se cambia si viene un valor válido (este PUT también lo usan pantallas que no lo envían).
                if (!string.IsNullOrEmpty(request.DecimalSeparator))
                {
                    if (!DecimalSeparators.IsValid(request.DecimalSeparator))
                        return BadRequest(new { Message = "El separador decimal debe ser \".\" o \",\"." });
                    client.DecimalSeparator = request.DecimalSeparator;
                }

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
        public string? UnitOfMeasureDisplayOverride { get; set; }
        public string? DecimalSeparator { get; set; }
    }
}
