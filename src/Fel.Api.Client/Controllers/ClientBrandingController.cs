using System;
using System.Threading.Tasks;
using Fel.Core.Interfaces;
using Fel.Core.Models;
using Fel.Infrastructure.Data;
using Fel.Infrastructure.Email;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Fel.Api.Security;
using Fel.Core.Entities;

namespace Fel.Api.Client.Controllers
{
    [ApiController]
    [Route("api/v1/branding")]
    [AllowAllBranches]
    public class ClientBrandingController : ClientPortalControllerBase
    {
        private const string ApiBaseUrl = "https://api.facil-factura.pro";
        private readonly FelDbContext _dbContext;
        private readonly IPublicFileStorageService _fileStorage;

        public ClientBrandingController(FelDbContext dbContext, IPublicFileStorageService fileStorage)
        {
            _dbContext = dbContext;
            _fileStorage = fileStorage;
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
                    // NIT + DV del Client, que el header muestra debajo del nombre.
                    TaxId = client.TaxId,
                    VerificationDigit = client.VerificationDigit,
                    UnitOfMeasureDisplayOverride = client.UnitOfMeasureDisplayOverride,
                    // Formato numérico del portal y de los PDF de este cliente: "." (punto decimal) o ",".
                    DecimalSeparator = client.DecimalSeparator,
                    // Colores propios de los correos de documentos (nulos = heredar) y el color que se usaría si no definiera los suyos.
                    EmailColorLight = client.EmailColorLight,
                    EmailColorDark = client.EmailColorDark,
                    InheritedEmailColor = DocumentEmailColors.Resolve(
                        (client.Tenant.EmailColorLight, client.Tenant.EmailColorDark),
                        await DocumentEmailColors.PlatformColorsAsync(_dbContext))
                };

                return Ok(branding);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(ex.Message);
            }
        }

        [ClientRole(ClientUserRoles.Administrator)]
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
        [ClientRole(ClientUserRoles.Administrator)]
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

        // Colores de los correos con los que se entregan los documentos. Vacío = heredar los del tenant.
        [ClientRole(ClientUserRoles.Administrator)]
        [HttpPut("email-colors")]
        public async Task<IActionResult> UpdateEmailColors([FromBody] EmailColorsRequest request)
        {
            try
            {
                var clientId = GetCurrentClientId();
                var client = await _dbContext.Clients.FirstOrDefaultAsync(c => c.Id == clientId);
                if (client == null) return NotFound(new { Message = "Client not found" });

                if (!TryNormalize(request, out var light, out var dark)) return BadRequest(new { Message = "Los colores deben tener el formato #rrggbb." });
                client.EmailColorLight = light;
                client.EmailColorDark = dark;
                await _dbContext.SaveChangesAsync();
                return Ok(new { Message = "Colores guardados." });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(ex.Message);
            }
        }

        // Cómo se vería el correo con esos colores (sin guardarlos), para decidir sin enviar uno de prueba. Usa los datos reales del emisor y un
        // documento de ejemplo.
        [HttpPost("email-preview")]
        public async Task<IActionResult> PreviewEmail([FromBody] EmailColorsRequest request)
        {
            try
            {
                var clientId = GetCurrentClientId();
                var client = await _dbContext.Clients.AsNoTracking().FirstOrDefaultAsync(c => c.Id == clientId);
                if (client == null) return NotFound(new { Message = "Client not found" });

                if (!TryNormalize(request, out var light, out var dark)) return BadRequest(new { Message = "Los colores deben tener el formato #rrggbb." });
                var background = DocumentEmailColors.Resolve(
                    (light, dark),
                    await DocumentEmailColors.TenantColorsAsync(_dbContext, client.TenantId),
                    await DocumentEmailColors.PlatformColorsAsync(_dbContext));
                var html = DocumentEmailTemplate.Build(new DocumentEmailModel(
                    string.IsNullOrWhiteSpace(client.CommercialName) ? client.CompanyName : client.CommercialName,
                    string.IsNullOrWhiteSpace(client.VerificationDigit) ? client.TaxId : $"{client.TaxId}-{client.VerificationDigit}",
                    client.Address, client.Phone, client.Email,
                    !string.IsNullOrWhiteSpace(client.LogoLightUrl) ? client.LogoLightUrl : client.LogoDarkUrl,
                    "factura electrónica de venta", "FE1001", DateTime.Today, 1250000m, "COP", background));
                return Ok(new { html });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(ex.Message);
            }
        }

        // Normaliza los dos colores; falla si alguno viene con texto que no es #rrggbb.
        private static bool TryNormalize(EmailColorsRequest request, out string? light, out string? dark)
        {
            light = DocumentEmailColors.Normalize(request.ColorLight);
            dark = DocumentEmailColors.Normalize(request.ColorDark);
            return (string.IsNullOrWhiteSpace(request.ColorLight) || light != null) && (string.IsNullOrWhiteSpace(request.ColorDark) || dark != null);
        }
    }

    public class EmailColorsRequest
    {
        // Vacío o nulo = heredar (tenant y luego plataforma).
        public string? ColorLight { get; set; }
        public string? ColorDark { get; set; }
    }

    public class UpdateClientBrandingRequest
    {
        public string? LogoLightUrl { get; set; }
        public string? LogoDarkUrl { get; set; }
        public string? UnitOfMeasureDisplayOverride { get; set; }
        public string? DecimalSeparator { get; set; }
    }
}
