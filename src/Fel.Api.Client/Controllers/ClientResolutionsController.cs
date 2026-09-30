using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using Fel.Core.Entities;
using Fel.Infrastructure.Data;
using Fel.Infrastructure.Services;

namespace Fel.Api.Client.Controllers
{
    [ApiController]
    [Route("api/client/resolutions")]
    public class ClientResolutionsController : ControllerBase
    {
        private readonly FelDbContext _dbContext;
        private readonly DianResolutionParserService _parserService;

        public ClientResolutionsController(FelDbContext dbContext, DianResolutionParserService parserService)
        {
            _dbContext = dbContext;
            _parserService = parserService;
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
        public async Task<IActionResult> GetResolutions()
        {
            try
            {
                var clientId = GetCurrentClientId();

                var resolutions = await _dbContext.Resolutions
                    .Where(r => r.ClientId == clientId && r.IsActive)
                    .OrderByDescending(r => r.ValidFrom)
                    .Select(r => new
                    {
                        r.Id,
                        r.ResolutionNumber,
                        r.Prefix,
                        r.NumberStart,
                        r.NumberEnd,
                        r.ValidFrom,
                        r.ValidTo,
                        r.TechnicalKey,
                        r.DocumentType,
                        r.NextNumber,
                        r.IsDefault
                    })
                    .ToListAsync();

                return Ok(resolutions);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPost("parse")]
        public async Task<IActionResult> ParsePdf(IFormFile file)
        {
            try
            {
                GetCurrentClientId(); // Validar autenticación

                if (file == null || file.Length == 0)
                    return BadRequest("No se proporcionó un archivo PDF válido.");

                if (file.ContentType != "application/pdf")
                    return BadRequest("El archivo debe ser un PDF.");

                using var stream = file.OpenReadStream();
                var results = await _parserService.ParsePdfAsync(stream);

                if (results.Count == 1 && !results[0].IsSuccess)
                {
                    return BadRequest(results[0].ErrorMessage);
                }

                return Ok(results);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno procesando el archivo: {ex.Message}");
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

        [HttpPost]
        public async Task<IActionResult> CreateResolution([FromBody] CreateResolutionRequest request)
        {
            try
            {
                var clientId = GetCurrentClientId();

                // Desactivar las anteriores del mismo tipo y prefijo
                var existingActive = await _dbContext.Resolutions
                    .Where(r => r.ClientId == clientId && 
                                r.DocumentType == request.DocumentType && 
                                r.Prefix == request.Prefix && 
                                r.IsActive)
                    .ToListAsync();

                foreach (var res in existingActive)
                {
                    res.IsActive = false;
                }

                // Si es la primera resolución activa de este tipo de documento para el cliente, se
                // marca como predeterminada de una vez — así nunca queda un tipo sin default cuando
                // solo hay una opción.
                var hasOtherActiveOfType = await _dbContext.Resolutions
                    .AnyAsync(r => r.ClientId == clientId && r.DocumentType == request.DocumentType && r.IsActive);

                var resolution = new Resolution
                {
                    Id = Guid.NewGuid(),
                    ClientId = clientId,
                    ResolutionNumber = request.ResolutionNumber?.Trim() ?? "",
                    Prefix = request.Prefix?.Trim() ?? "",
                    NumberStart = request.NumberStart,
                    NumberEnd = request.NumberEnd,
                    ValidFrom = request.ValidFrom,
                    ValidTo = request.ValidTo,
                    TechnicalKey = request.TechnicalKey ?? "",
                    DocumentType = request.DocumentType,
                    IsActive = true,
                    IsDefault = !hasOtherActiveOfType
                };

                _dbContext.Resolutions.Add(resolution);
                await _dbContext.SaveChangesAsync();

                return Ok(new {
                    resolution.Id,
                    resolution.ResolutionNumber,
                    resolution.Prefix,
                    resolution.NumberStart,
                    resolution.NumberEnd,
                    resolution.ValidFrom,
                    resolution.ValidTo,
                    resolution.DocumentType,
                    resolution.IsDefault
                });
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }
        
        public class UpdateResolutionRequest
        {
            public string ResolutionNumber { get; set; } = string.Empty;
            public string Prefix { get; set; } = string.Empty;
            public long NumberStart { get; set; }
            public long NumberEnd { get; set; }
            public DateTime ValidFrom { get; set; }
            public DateTime ValidTo { get; set; }
            public string TechnicalKey { get; set; } = string.Empty;
        }

        // No incluye DocumentType a propósito: cambiar el tipo de una resolución ya en uso arrastra
        // la numeración y el default por tipo. Si hace falta otro tipo, se crea una resolución nueva.
        [HttpPut("{id:guid}")]
        public async Task<IActionResult> UpdateResolution(Guid id, [FromBody] UpdateResolutionRequest request)
        {
            var clientId = GetCurrentClientId();

            var resolution = await _dbContext.Resolutions.FirstOrDefaultAsync(r => r.Id == id && r.ClientId == clientId);
            if (resolution == null) return NotFound();

            // El próximo consecutivo a usar no se toca acá (tiene su propio endpoint), pero el
            // rango editado tiene que seguir conteniéndolo — si no, la próxima factura emitida
            // quedaría fuera del rango autorizado sin que nadie lo note hasta que la DIAN la rechace.
            var nextNumber = resolution.NextNumber ?? resolution.NumberStart;
            if (nextNumber < request.NumberStart || nextNumber > request.NumberEnd)
            {
                return BadRequest($"El rango debe seguir incluyendo el próximo número a usar ({nextNumber}).");
            }

            resolution.ResolutionNumber = request.ResolutionNumber?.Trim() ?? "";
            resolution.Prefix = request.Prefix?.Trim() ?? "";
            resolution.NumberStart = request.NumberStart;
            resolution.NumberEnd = request.NumberEnd;
            resolution.ValidFrom = request.ValidFrom;
            resolution.ValidTo = request.ValidTo;
            resolution.TechnicalKey = request.TechnicalKey ?? "";
            await _dbContext.SaveChangesAsync();

            return Ok(new
            {
                resolution.Id,
                resolution.ResolutionNumber,
                resolution.Prefix,
                resolution.NumberStart,
                resolution.NumberEnd,
                resolution.ValidFrom,
                resolution.ValidTo,
                resolution.TechnicalKey,
                resolution.DocumentType,
                resolution.NextNumber,
                resolution.IsDefault
            });
        }

        public class SetNextNumberRequest
        {
            public long NextNumber { get; set; }
        }

        // Permite fijar manualmente el próximo consecutivo a usar — ej. al migrar desde otro
        // sistema donde ya se emitieron facturas hasta cierto número.
        [HttpPut("{id:guid}/next-number")]
        public async Task<IActionResult> SetNextNumber(Guid id, [FromBody] SetNextNumberRequest request)
        {
            var clientId = GetCurrentClientId();

            var resolution = await _dbContext.Resolutions.FirstOrDefaultAsync(r => r.Id == id && r.ClientId == clientId);
            if (resolution == null) return NotFound();

            if (request.NextNumber < resolution.NumberStart || request.NextNumber > resolution.NumberEnd)
            {
                return BadRequest($"El número debe estar entre {resolution.NumberStart} y {resolution.NumberEnd} (el rango autorizado de esta resolución).");
            }

            resolution.NextNumber = request.NextNumber;
            await _dbContext.SaveChangesAsync();

            return Ok(new { resolution.Id, resolution.NextNumber });
        }

        // Marca esta resolución como la predeterminada de su tipo de documento, desmarcando
        // cualquier otra resolución activa del mismo ClientId+DocumentType.
        [HttpPut("{id:guid}/set-default")]
        public async Task<IActionResult> SetDefault(Guid id)
        {
            var clientId = GetCurrentClientId();

            var resolution = await _dbContext.Resolutions.FirstOrDefaultAsync(r => r.Id == id && r.ClientId == clientId && r.IsActive);
            if (resolution == null) return NotFound();

            var others = await _dbContext.Resolutions
                .Where(r => r.ClientId == clientId && r.DocumentType == resolution.DocumentType && r.IsActive && r.Id != id)
                .ToListAsync();

            foreach (var other in others)
            {
                other.IsDefault = false;
            }

            resolution.IsDefault = true;
            await _dbContext.SaveChangesAsync();

            return Ok(new { resolution.Id, resolution.IsDefault });
        }

        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> DeleteResolution(Guid id)
        {
            var clientId = GetCurrentClientId();

            var resolution = await _dbContext.Resolutions.FirstOrDefaultAsync(r => r.Id == id && r.ClientId == clientId);
            if (resolution == null) return NotFound();

            resolution.IsActive = false;
            await _dbContext.SaveChangesAsync();

            return NoContent();
        }

        // Consecutivo interno de Notas Crédito/Débito — separado del NextNumber de cualquier
        // Resolution porque las notas no tienen rango autorizado propio ante la DIAN (ver
        // ResolutionNumbering.ClaimNextCreditNoteNumberAsync/ClaimNextDebitNoteNumberAsync). Ruta
        // bajo /resolutions a propósito: HmacAuthenticationMiddleware ya exime ese prefijo para
        // que el portal de cliente use su sesión (x-client-id) en vez de HMAC.
        [HttpGet("note-counters")]
        public async Task<IActionResult> GetNoteCounters()
        {
            var clientId = GetCurrentClientId();
            var client = await _dbContext.Clients.FirstOrDefaultAsync(c => c.Id == clientId);
            if (client == null) return NotFound();

            return Ok(new
            {
                nextCreditNoteNumber = client.NextCreditNoteNumber ?? 1,
                nextDebitNoteNumber = client.NextDebitNoteNumber ?? 1
            });
        }

        public class UpdateNoteCountersRequest
        {
            public long? NextCreditNoteNumber { get; set; }
            public long? NextDebitNoteNumber { get; set; }
        }

        [HttpPut("note-counters")]
        public async Task<IActionResult> UpdateNoteCounters([FromBody] UpdateNoteCountersRequest request)
        {
            var clientId = GetCurrentClientId();
            var client = await _dbContext.Clients.FirstOrDefaultAsync(c => c.Id == clientId);
            if (client == null) return NotFound();

            if (request.NextCreditNoteNumber.HasValue)
            {
                if (request.NextCreditNoteNumber.Value < 1) return BadRequest("El consecutivo de Nota Crédito debe ser mayor a 0.");
                client.NextCreditNoteNumber = request.NextCreditNoteNumber.Value;
            }
            if (request.NextDebitNoteNumber.HasValue)
            {
                if (request.NextDebitNoteNumber.Value < 1) return BadRequest("El consecutivo de Nota Débito debe ser mayor a 0.");
                client.NextDebitNoteNumber = request.NextDebitNoteNumber.Value;
            }

            await _dbContext.SaveChangesAsync();

            return Ok(new
            {
                nextCreditNoteNumber = client.NextCreditNoteNumber ?? 1,
                nextDebitNoteNumber = client.NextDebitNoteNumber ?? 1
            });
        }
    }
}
