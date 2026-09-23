using System;
using System.Linq;
using System.Threading.Tasks;
using Fel.Core.Entities;
using Fel.Infrastructure.Data;
using Fel.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Fel.Api.Tenant.Controllers
{
    // El Tenant invita a su equipo de developers al portal de developers
    // (developers.facil-factura.pro). Todos los developers invitados por un mismo Tenant
    // comparten un único Client de prueba (IsDeveloperSandbox=true, ver DeveloperUser en
    // Fel.Core y DeveloperAuthController en Fel.Api.Client), auto-provisionado la primera vez
    // que se invita a alguien — así el equipo prueba su integración de forma aislada, sin tocar
    // los Clients reales del Tenant.
    [ApiController]
    [Route("api/tenant/developers")]
    public class TenantDevelopersController : ControllerBase
    {
        // Mismo Integrator por defecto (DIAN directa) que usa TenantClientsController al crear
        // un Client nuevo.
        private static readonly Guid DefaultIntegratorId = Guid.Parse("00000000-0000-0000-0000-000000000101");

        private readonly FelDbContext _dbContext;
        private readonly PasswordResetService _passwordResetService;
        private readonly string _developerPortalUrl;

        public TenantDevelopersController(FelDbContext dbContext, PasswordResetService passwordResetService, IConfiguration config)
        {
            _dbContext = dbContext;
            _passwordResetService = passwordResetService;
            _developerPortalUrl = config["DeveloperPortalUrl"] ?? "https://developers.facil-factura.pro";
        }

        private Guid GetCurrentTenantId()
        {
            if (Request.Headers.TryGetValue("x-tenant-id", out var tenantIdStr) && Guid.TryParse(tenantIdStr, out var tenantId))
            {
                return tenantId;
            }
            throw new UnauthorizedAccessException("x-tenant-id Header is missing");
        }

        [HttpGet]
        public async Task<IActionResult> GetDevelopers()
        {
            try
            {
                var tenantId = GetCurrentTenantId();
                var developers = await _dbContext.DeveloperUsers
                    .Where(d => d.TenantId == tenantId)
                    .OrderBy(d => d.Name)
                    .Select(d => new { d.Id, d.Name, d.Email, d.IsActive, d.CreatedAt })
                    .ToListAsync();

                return Ok(developers);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(ex.Message);
            }
        }

        [HttpPost]
        public async Task<IActionResult> InviteDeveloper([FromBody] InviteDeveloperRequest request)
        {
            try
            {
                var tenantId = GetCurrentTenantId();

                if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Email))
                {
                    return BadRequest("Nombre y correo son obligatorios.");
                }

                if (await _dbContext.DeveloperUsers.AnyAsync(d => d.Email == request.Email))
                {
                    return BadRequest("Ese correo ya está registrado en el portal de developers.");
                }

                var tenant = await _dbContext.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId);
                if (tenant == null) return NotFound("Tenant no encontrado.");

                var sandboxClient = await GetOrCreateTenantSandboxClientAsync(tenant);

                var developer = new DeveloperUser
                {
                    Id = Guid.NewGuid(),
                    Name = request.Name,
                    Email = request.Email,
                    // Sin contraseña manual: hash aleatorio inutilizable, el developer la
                    // establece él mismo desde el enlace de invitación (mismo patrón que
                    // TenantClientsController.UpsertPortalUser para ClientUser).
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString()),
                    TenantId = tenantId,
                    ClientId = sandboxClient.Id,
                    CreatedAt = DateTime.UtcNow,
                    IsActive = true
                };
                _dbContext.DeveloperUsers.Add(developer);
                await _dbContext.SaveChangesAsync();

                await _passwordResetService.RequestAsync(
                    PortalUserType.Developer, developer.Id, developer.Email, developer.Name, _developerPortalUrl, "invitation",
                    tenant.CoreTenantId, tenant.LogoLightUrl, tenant.CommercialName);

                return Ok(new { developer.Id, developer.Name, developer.Email, developer.IsActive });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(ex.Message);
            }
        }

        [HttpPost("{id}/resend-invitation")]
        public async Task<IActionResult> ResendInvitation(Guid id)
        {
            try
            {
                var tenantId = GetCurrentTenantId();
                var developer = await _dbContext.DeveloperUsers.FirstOrDefaultAsync(d => d.Id == id && d.TenantId == tenantId);
                if (developer == null) return NotFound();

                if (!developer.IsActive)
                {
                    return BadRequest("Este developer fue revocado; no se puede reenviar la invitación.");
                }

                var tenant = await _dbContext.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId);
                if (tenant == null) return NotFound("Tenant no encontrado.");

                await _passwordResetService.RequestAsync(
                    PortalUserType.Developer, developer.Id, developer.Email, developer.Name, _developerPortalUrl, "invitation",
                    tenant.CoreTenantId, tenant.LogoLightUrl, tenant.CommercialName);

                return Ok(new { message = "Invitación reenviada." });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(ex.Message);
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> RevokeDeveloper(Guid id)
        {
            try
            {
                var tenantId = GetCurrentTenantId();
                var developer = await _dbContext.DeveloperUsers.FirstOrDefaultAsync(d => d.Id == id && d.TenantId == tenantId);
                if (developer == null) return NotFound();

                developer.IsActive = false;
                await _dbContext.SaveChangesAsync();

                return NoContent();
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(ex.Message);
            }
        }

        [HttpPost("{id}/reactivate")]
        public async Task<IActionResult> ReactivateDeveloper(Guid id)
        {
            try
            {
                var tenantId = GetCurrentTenantId();
                var developer = await _dbContext.DeveloperUsers.FirstOrDefaultAsync(d => d.Id == id && d.TenantId == tenantId);
                if (developer == null) return NotFound();

                developer.IsActive = true;
                await _dbContext.SaveChangesAsync();

                return Ok(new { developer.Id, developer.Name, developer.Email, developer.IsActive });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(ex.Message);
            }
        }

        // Un solo Client de prueba por Tenant, compartido entre todos los developers que ese
        // Tenant invite — se crea la primera vez que se invita a alguien.
        private async Task<Client> GetOrCreateTenantSandboxClientAsync(Fel.Core.Entities.Tenant tenant)
        {
            var existing = await _dbContext.Clients.FirstOrDefaultAsync(c => c.TenantId == tenant.Id && c.IsDeveloperSandbox);
            if (existing != null) return existing;

            var sandboxClient = new Client
            {
                Id = Guid.NewGuid(),
                TenantId = tenant.Id,
                CompanyName = $"Sandbox Developers - {tenant.CommercialName}",
                CommercialName = $"Sandbox Developers - {tenant.CommercialName}",
                Email = tenant.Email,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                IntegratorId = DefaultIntegratorId,
                IsDeveloperSandbox = true
            };
            _dbContext.Clients.Add(sandboxClient);

            _dbContext.ClientIntegratorAssignments.Add(new ClientIntegratorAssignment
            {
                Id = Guid.NewGuid(),
                ClientId = sandboxClient.Id,
                IntegratorId = sandboxClient.IntegratorId,
                EffectiveFrom = sandboxClient.CreatedAt,
                EffectiveTo = null
            });

            foreach (var documentTypeId in DefaultCatalogSets.StandardDocumentTypeIds)
            {
                _dbContext.ClientEnabledDocumentTypes.Add(new ClientEnabledDocumentType
                {
                    Id = Guid.NewGuid(),
                    ClientId = sandboxClient.Id,
                    DocumentTypeId = documentTypeId
                });
            }
            foreach (var retentionConceptId in DefaultCatalogSets.StandardRetentionConceptIds)
            {
                _dbContext.ClientEnabledRetentionConcepts.Add(new ClientEnabledRetentionConcept
                {
                    Id = Guid.NewGuid(),
                    ClientId = sandboxClient.Id,
                    RetentionConceptId = retentionConceptId
                });
            }

            return sandboxClient;
        }
    }

    public class InviteDeveloperRequest
    {
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
    }
}
