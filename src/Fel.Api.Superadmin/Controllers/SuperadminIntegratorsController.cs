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
    [Route("api/superadmin/integrators")]
    public class SuperadminIntegratorsController : ControllerBase
    {
        private readonly FelDbContext _dbContext;

        public SuperadminIntegratorsController(FelDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        [HttpGet]
        public async Task<IActionResult> GetIntegrators()
        {
            var integrators = await _dbContext.Integrators
                .Select(i => new { i.Id, i.Code, i.Name, i.Nit, i.Kind, i.IsActive })
                .ToListAsync();
            return Ok(integrators);
        }

        [HttpPost]
        public async Task<IActionResult> CreateIntegrator([FromBody] IntegratorRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(request.Name))
            {
                return BadRequest("Código y nombre son obligatorios.");
            }

            var code = request.Code.Trim().ToUpper();
            if (await _dbContext.Integrators.AnyAsync(i => i.Code == code))
            {
                return BadRequest("El código de integrador ya existe.");
            }

            var integrator = new Integrator
            {
                Id = Guid.NewGuid(),
                Code = code,
                Name = request.Name.Trim(),
                Nit = request.Nit?.Trim() ?? string.Empty,
                Kind = request.Kind,
                IsActive = true
            };

            _dbContext.Integrators.Add(integrator);
            await _dbContext.SaveChangesAsync();

            return Ok(integrator);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateIntegrator(Guid id, [FromBody] IntegratorRequest request)
        {
            var integrator = await _dbContext.Integrators.FindAsync(id);
            if (integrator == null) return NotFound();

            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return BadRequest("El nombre es obligatorio.");
            }

            integrator.Name = request.Name.Trim();
            integrator.Nit = request.Nit?.Trim() ?? string.Empty;
            integrator.Kind = request.Kind;
            await _dbContext.SaveChangesAsync();

            return Ok(integrator);
        }

        [HttpPut("{id}/active")]
        public async Task<IActionResult> SetActive(Guid id, [FromBody] SetActiveRequest request)
        {
            var integrator = await _dbContext.Integrators.FindAsync(id);
            if (integrator == null) return NotFound();

            integrator.IsActive = request.IsActive;
            await _dbContext.SaveChangesAsync();

            return Ok(integrator);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteIntegrator(Guid id)
        {
            var integrator = await _dbContext.Integrators.FindAsync(id);
            if (integrator == null) return NotFound();

            if (integrator.Code == "NATIVE" || integrator.Code == "DATAICO")
            {
                return BadRequest("Este integrador es parte del catálogo base y no se puede eliminar. Desactívalo en su lugar.");
            }

            if (await _dbContext.Clients.AnyAsync(c => c.IntegratorId == id))
            {
                return BadRequest("No se puede eliminar porque ya tiene Clients asociados.");
            }

            _dbContext.Integrators.Remove(integrator);
            await _dbContext.SaveChangesAsync();

            return Ok(new { message = "Integrador eliminado" });
        }
    }

    public class IntegratorRequest
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Nit { get; set; }
        public IntegratorKind Kind { get; set; } = IntegratorKind.DirectDian;
    }

    public class SetActiveRequest
    {
        public bool IsActive { get; set; }
    }
}
