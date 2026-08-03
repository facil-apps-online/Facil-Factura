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
    [Route("api/superadmin/document-types")]
    public class SuperadminDocumentTypesController : ControllerBase
    {
        private readonly FelDbContext _dbContext;

        public SuperadminDocumentTypesController(FelDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        [HttpGet]
        public async Task<IActionResult> GetDocumentTypes()
        {
            var types = await _dbContext.DocumentTypes
                .Select(d => new { d.Id, d.Code, d.Name, d.IsActive, d.GoverningEntity })
                .ToListAsync();
            return Ok(types);
        }

        [HttpPost]
        public async Task<IActionResult> CreateDocumentType([FromBody] DocumentTypeRequest request)
        {
            if (await _dbContext.DocumentTypes.AnyAsync(d => d.Code == request.Code))
            {
                return BadRequest("El código de documento ya existe.");
            }

            var docType = new DocumentType
            {
                Id = Guid.NewGuid(),
                Code = request.Code.ToUpper(),
                Name = request.Name,
                GoverningEntity = string.IsNullOrEmpty(request.GoverningEntity) ? "DIAN" : request.GoverningEntity.ToUpper(),
                IsActive = true
            };

            _dbContext.DocumentTypes.Add(docType);
            await _dbContext.SaveChangesAsync();

            return Ok(docType);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteDocumentType(Guid id)
        {
            var docType = await _dbContext.DocumentTypes.FindAsync(id);
            if (docType == null) return NotFound();

            // Check if it's used in pricings
            if (await _dbContext.TenantPricings.AnyAsync(p => p.DocumentTypeId == id))
            {
                return BadRequest("No se puede eliminar porque ya tiene tarifas asociadas a Tenants.");
            }

            _dbContext.DocumentTypes.Remove(docType);
            await _dbContext.SaveChangesAsync();

            return Ok(new { message = "Documento eliminado" });
        }
    }

    public class DocumentTypeRequest
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string GoverningEntity { get; set; } = "DIAN";
    }
}
