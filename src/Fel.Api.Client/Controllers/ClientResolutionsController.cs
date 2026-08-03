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
                        r.DocumentType
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
                var result = await _parserService.ParsePdfAsync(stream);

                if (!result.IsSuccess)
                {
                    return BadRequest(result.ErrorMessage);
                }

                return Ok(result);
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
                    resolution.DocumentType
                });
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
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
    }
}
