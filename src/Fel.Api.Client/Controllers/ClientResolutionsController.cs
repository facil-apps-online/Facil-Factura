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
using Fel.Api.Security;
using System.Collections.Generic;

namespace Fel.Api.Client.Controllers
{
    [ApiController]
    [Route("api/client/resolutions")]
    [AllowAllBranches]
    public class ClientResolutionsController : ClientPortalControllerBase
    {
        private readonly FelDbContext _dbContext;
        private readonly DianResolutionParserService _parserService;

        public ClientResolutionsController(FelDbContext dbContext, DianResolutionParserService parserService)
        {
            _dbContext = dbContext;
            _parserService = parserService;
        }

        [HttpGet]
        public async Task<IActionResult> GetResolutions()
        {
            try
            {
                var clientId = GetCurrentClientId();

                var resolutions = await _dbContext.Resolutions.ForBranch(_dbContext, CurrentBranchScope)
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
                        r.IsDefault,
                        BranchIds = _dbContext.ResolutionBranches.Where(rb => rb.ResolutionId == r.Id).Select(rb => rb.BranchId).ToList()
                    })
                    .ToListAsync();

                return Ok(resolutions);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [ClientRole(ClientUserRoles.Administrator)]
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
            // Sucursales donde se usará. Sin elegir: las de la resolución que reemplaza o, si no hay, la sucursal activa.
            public List<Guid>? BranchIds { get; set; }
        }

        [ClientRole(ClientUserRoles.Administrator)]
        [HttpPost]
        public async Task<IActionResult> CreateResolution([FromBody] CreateResolutionRequest request)
        {
            try
            {
                var clientId = GetCurrentClientId();

                var (resolutionBranchIds, branchError) = await ResolveResolutionBranchesAsync(clientId, request.BranchIds, request.DocumentType, request.Prefix);
                if (branchError != null) return BadRequest(branchError);

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
                foreach (var branchId in resolutionBranchIds)
                    _dbContext.ResolutionBranches.Add(BranchProvisioning.LinkResolution(resolution.Id, branchId));
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
        [ClientRole(ClientUserRoles.Administrator)]
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
        [ClientRole(ClientUserRoles.Administrator)]
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
        [ClientRole(ClientUserRoles.Administrator)]
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

        [ClientRole(ClientUserRoles.Administrator)]
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
        // ResolutionNumbering.ClaimNextNoteAsync). Ruta
        // bajo /resolutions a propósito: HmacAuthenticationMiddleware ya exime ese prefijo para
        // que el portal de cliente use su sesión (x-client-id) en vez de HMAC.
        public class DocumentLegendRequest
        {
            public string? Text { get; set; }
        }

        [ClientRole(ClientUserRoles.Administrator)]
        [HttpGet("{id:guid}/legend")]
        public async Task<IActionResult> GetLegend(Guid id)
        {
            var clientId = GetCurrentClientId();
            var resolution = await _dbContext.Resolutions.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id && r.ClientId == clientId);
            if (resolution == null) return NotFound();
            var type = Fel.Infrastructure.Services.DocumentLegendType.Normalize(resolution.DocumentType);
            var legend = await _dbContext.DocumentLegendByPrefixes.AsNoTracking().FirstOrDefaultAsync(x => x.ClientId == clientId && x.DocumentType == type && x.Prefix == resolution.Prefix);
            return Ok(new { documentType = type, prefix = resolution.Prefix, text = legend?.Text ?? string.Empty });
        }

        [ClientRole(ClientUserRoles.Administrator)]
        [HttpPut("{id:guid}/legend")]
        public async Task<IActionResult> UpdateLegend(Guid id, [FromBody] DocumentLegendRequest request)
        {
            var clientId = GetCurrentClientId();
            var resolution = await _dbContext.Resolutions.FirstOrDefaultAsync(r => r.Id == id && r.ClientId == clientId);
            if (resolution == null) return NotFound();
            var type = Fel.Infrastructure.Services.DocumentLegendType.Normalize(resolution.DocumentType);
            var legend = await _dbContext.DocumentLegendByPrefixes.FirstOrDefaultAsync(x => x.ClientId == clientId && x.DocumentType == type && x.Prefix == resolution.Prefix);
            var text = request.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(text))
            {
                if (legend != null) _dbContext.DocumentLegendByPrefixes.Remove(legend);
            }
            else if (legend == null)
            {
                _dbContext.DocumentLegendByPrefixes.Add(new DocumentLegendByPrefix { Id = Guid.NewGuid(), ClientId = clientId, DocumentType = type, Prefix = resolution.Prefix.Trim(), Text = text, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
            }
            else
            {
                legend.Text = text;
                legend.UpdatedAt = DateTime.UtcNow;
            }
            await _dbContext.SaveChangesAsync();
            return Ok(new { documentType = type, prefix = resolution.Prefix, text });
        }

        public class SetResolutionBranchesRequest
        {
            public List<Guid> BranchIds { get; set; } = new List<Guid>();
        }

        // Reemplaza las sucursales donde se usa la resolución. Debe quedar al menos una: sin sucursal nadie podría emitir con ella.
        [ClientRole(ClientUserRoles.Administrator)]
        [HttpPut("{id:guid}/branches")]
        public async Task<IActionResult> SetBranches(Guid id, [FromBody] SetResolutionBranchesRequest request)
        {
            var clientId = GetCurrentClientId();
            if (!await _dbContext.Resolutions.AnyAsync(r => r.Id == id && r.ClientId == clientId)) return NotFound();

            var requested = request.BranchIds.Distinct().ToList();
            if (requested.Count == 0) return BadRequest("La resolución debe estar disponible en al menos una sucursal.");

            var (valid, error) = await ValidBranchesAsync(clientId, requested);
            if (error != null) return BadRequest(error);

            var current = await _dbContext.ResolutionBranches.Where(rb => rb.ResolutionId == id).ToListAsync();
            _dbContext.ResolutionBranches.RemoveRange(current.Where(rb => !valid.Contains(rb.BranchId)));
            foreach (var branchId in valid.Where(b => current.All(rb => rb.BranchId != b)))
                _dbContext.ResolutionBranches.Add(BranchProvisioning.LinkResolution(id, branchId));

            await _dbContext.SaveChangesAsync();
            return Ok(new { branchIds = valid });
        }

        // Sucursales donde se usará una resolución nueva: las elegidas; si no se eligió ninguna, las de la resolución que reemplaza
        // (mismo tipo y prefijo, para no dejar sin ella a las demás sucursales) y, si no hay, la sucursal activa.
        private async Task<(List<Guid> Ids, string? Error)> ResolveResolutionBranchesAsync(Guid clientId, List<Guid>? requested, string documentType, string prefix)
        {
            var ids = (requested ?? new List<Guid>()).Distinct().ToList();
            if (ids.Count == 0)
            {
                ids = await _dbContext.ResolutionBranches
                    .Where(rb => rb.Resolution.ClientId == clientId && rb.Resolution.DocumentType == documentType && rb.Resolution.Prefix == prefix && rb.Resolution.IsActive)
                    .Select(rb => rb.BranchId).Distinct().ToListAsync();
                if (ids.Count == 0 && CurrentBranchScope is Guid current) ids.Add(current);
                if (ids.Count == 0) return (ids, "Elige las sucursales donde se usará la resolución.");
            }
            return await ValidBranchesAsync(clientId, ids);
        }

        // Las sucursales deben ser del Client y estar activas.
        private async Task<(List<Guid> Ids, string? Error)> ValidBranchesAsync(Guid clientId, List<Guid> ids)
        {
            var valid = await _dbContext.Branches.AsNoTracking()
                .Where(b => b.ClientId == clientId && b.IsActive && ids.Contains(b.Id))
                .Select(b => b.Id).ToListAsync();
            return valid.Count == ids.Count ? (valid, null) : (valid, "Alguna de las sucursales elegidas no existe o está inactiva.");
        }

        [ClientRole(ClientUserRoles.Administrator)]
        [HttpGet("note-counters")]
        public async Task<IActionResult> GetNoteCounters()
        {
            return Ok(ToNoteCountersResponse(await NoteNumberingService.GetSharedAsync(_dbContext, GetCurrentClientId())));
        }

        private static object ToNoteCountersResponse(NoteNumberingService.SharedCounters counters) => new
        {
            nextCreditNoteNumber = counters.NextCreditNoteNumber,
            nextDebitNoteNumber = counters.NextDebitNoteNumber,
            nextSupportAdjustmentNumber = counters.NextSupportAdjustmentNumber,
            supportAdjustmentPrefix = counters.SupportAdjustmentPrefix
        };

        public class UpdateNoteCountersRequest
        {
            public long? NextCreditNoteNumber { get; set; }
            public long? NextDebitNoteNumber { get; set; }
            public long? NextSupportAdjustmentNumber { get; set; }
            // null = no cambiar; cadena vacía = quitar el prefijo configurado.
            public string? SupportAdjustmentPrefix { get; set; }
        }

        [ClientRole(ClientUserRoles.Administrator)]
        [HttpPut("note-counters")]
        public async Task<IActionResult> UpdateNoteCounters([FromBody] UpdateNoteCountersRequest request)
        {
            var clientId = GetCurrentClientId();

            if (request.NextCreditNoteNumber is < 1) return BadRequest("El consecutivo de Nota Crédito debe ser mayor a 0.");
            if (request.NextDebitNoteNumber is < 1) return BadRequest("El consecutivo de Nota Débito debe ser mayor a 0.");
            if (request.NextSupportAdjustmentNumber is < 1) return BadRequest("El consecutivo de Nota de Ajuste debe ser mayor a 0.");

            string? prefix = null;
            if (request.SupportAdjustmentPrefix != null)
            {
                prefix = request.SupportAdjustmentPrefix.Trim().ToUpperInvariant();
                if (prefix.Length > 10) return BadRequest("El prefijo de Nota de Ajuste no puede tener más de 10 caracteres.");
                if (prefix.Length > 0 && !prefix.All(char.IsLetterOrDigit)) return BadRequest("El prefijo de Nota de Ajuste solo puede tener letras y números.");
            }

            // Son los contadores compartidos del cliente; la numeración propia de una sucursal se administra aparte.
            var error = await NoteNumberingService.UpdateSharedAsync(_dbContext, clientId,
                request.NextCreditNoteNumber, request.NextDebitNoteNumber, request.NextSupportAdjustmentNumber, prefix);
            if (error != null) return BadRequest(error);

            await _dbContext.SaveChangesAsync();

            return Ok(ToNoteCountersResponse(await NoteNumberingService.GetSharedAsync(_dbContext, clientId)));
        }
    }
}
