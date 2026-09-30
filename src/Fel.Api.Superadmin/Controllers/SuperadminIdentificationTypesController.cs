using System;
using System.Linq;
using System.Threading.Tasks;
using Fel.Core.Entities;
using Fel.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Fel.Api.Superadmin.Controllers
{
    [ApiController]
    [Route("api/superadmin/identification-types")]
    public class SuperadminIdentificationTypesController : ControllerBase
    {
        private readonly FelDbContext _dbContext;

        public SuperadminIdentificationTypesController(FelDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        [HttpGet]
        public async Task<IActionResult> GetIdentificationTypes()
        {
            var types = await _dbContext.IdentificationTypes
                .OrderBy(t => t.Name)
                .ToListAsync();
            return Ok(types);
        }

        public class IdentificationTypeRequest
        {
            public string Code { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public bool IsActive { get; set; } = true;
            public string? DataicoCode { get; set; }
        }

        [HttpPost]
        public async Task<IActionResult> CreateIdentificationType([FromBody] IdentificationTypeRequest request)
        {
            var code = request.Code.Trim();
            if (await _dbContext.IdentificationTypes.AnyAsync(t => t.Code == code))
            {
                return BadRequest("Ya existe un tipo de identificación con ese código.");
            }

            var type = new IdentificationType
            {
                Id = Guid.NewGuid(),
                Code = code,
                Name = request.Name.Trim(),
                IsActive = true,
                DataicoCode = string.IsNullOrWhiteSpace(request.DataicoCode) ? null : request.DataicoCode.Trim()
            };

            _dbContext.IdentificationTypes.Add(type);
            await _dbContext.SaveChangesAsync();

            return Ok(type);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateIdentificationType(Guid id, [FromBody] IdentificationTypeRequest request)
        {
            var type = await _dbContext.IdentificationTypes.FindAsync(id);
            if (type == null) return NotFound();

            var code = request.Code.Trim();
            if (await _dbContext.IdentificationTypes.AnyAsync(t => t.Code == code && t.Id != id))
            {
                return BadRequest("Ya existe un tipo de identificación con ese código.");
            }

            type.Code = code;
            type.Name = request.Name.Trim();
            type.IsActive = request.IsActive;
            type.DataicoCode = string.IsNullOrWhiteSpace(request.DataicoCode) ? null : request.DataicoCode.Trim();
            await _dbContext.SaveChangesAsync();

            return Ok(type);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteIdentificationType(Guid id)
        {
            var type = await _dbContext.IdentificationTypes.FindAsync(id);
            if (type == null) return NotFound();

            // Es un catálogo de referencia (no hay FK desde Customer/Document), así que se elimina
            // directo — a diferencia de DocumentType, no hay pricing ni documentos que lo bloqueen.
            _dbContext.IdentificationTypes.Remove(type);
            await _dbContext.SaveChangesAsync();

            return Ok(new { message = "Tipo de identificación eliminado" });
        }
    }
}
