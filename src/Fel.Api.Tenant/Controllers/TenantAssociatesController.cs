using System;
using System.Linq;
using System.Threading.Tasks;
using Fel.Core.Entities;
using Fel.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Fel.Api.Tenant.Controllers
{
    // Asociados/comerciales del Tenant — registro informativo (sin acceso a portal) para saber a
    // qué comercial pertenece cada Client. Se asignan desde TenantClientsController (Client.AssociateId).
    [ApiController]
    [Route("api/tenant/associates")]
    public class TenantAssociatesController : ControllerBase
    {
        private readonly FelDbContext _dbContext;

        public TenantAssociatesController(FelDbContext dbContext)
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
        public async Task<IActionResult> GetAll()
        {
            try
            {
                var tenantId = GetCurrentTenantId();
                var associates = await _dbContext.Associates
                    .Where(a => a.TenantId == tenantId)
                    .OrderBy(a => a.Name)
                    .Select(a => new
                    {
                        a.Id,
                        a.Name,
                        a.Email,
                        a.Phone,
                        a.IsActive,
                        a.CreatedAt,
                        ClientCount = _dbContext.Clients.Count(c => c.AssociateId == a.Id)
                    })
                    .ToListAsync();

                return Ok(associates);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(ex.Message);
            }
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] AssociateRequest request)
        {
            try
            {
                var tenantId = GetCurrentTenantId();

                var associate = new Associate
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    Name = request.Name.Trim(),
                    Email = request.Email.Trim(),
                    Phone = request.Phone.Trim(),
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };

                _dbContext.Associates.Add(associate);
                await _dbContext.SaveChangesAsync();
                return Ok(associate);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(ex.Message);
            }
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] AssociateRequest request)
        {
            try
            {
                var tenantId = GetCurrentTenantId();
                var associate = await _dbContext.Associates.FirstOrDefaultAsync(a => a.Id == id && a.TenantId == tenantId);
                if (associate == null) return NotFound();

                associate.Name = request.Name.Trim();
                associate.Email = request.Email.Trim();
                associate.Phone = request.Phone.Trim();
                associate.IsActive = request.IsActive;

                await _dbContext.SaveChangesAsync();
                return Ok(associate);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(ex.Message);
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            try
            {
                var tenantId = GetCurrentTenantId();
                var associate = await _dbContext.Associates.FirstOrDefaultAsync(a => a.Id == id && a.TenantId == tenantId);
                if (associate == null) return NotFound();

                // La FK Clients.AssociateId no tiene cascada (SQL Server no permite múltiples rutas
                // de cascada hacia la misma tabla, porque Tenant ya cascada a Client directamente),
                // así que hay que desasignar explícitamente antes de poder borrar el asociado.
                var assignedClients = await _dbContext.Clients.Where(c => c.AssociateId == id).ToListAsync();
                foreach (var client in assignedClients) client.AssociateId = null;

                _dbContext.Associates.Remove(associate);
                await _dbContext.SaveChangesAsync();
                return Ok(new { message = "Asociado eliminado. Los clientes que tenía asignados quedan sin asociado." });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(ex.Message);
            }
        }
    }

    public class AssociateRequest
    {
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
    }
}
