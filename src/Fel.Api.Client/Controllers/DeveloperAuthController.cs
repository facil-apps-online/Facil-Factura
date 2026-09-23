using System;
using System.Threading.Tasks;
using Fel.Core.Entities;
using Fel.Core.Interfaces;
using Fel.Infrastructure.Data;
using Fel.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Fel.Api.Client.Controllers
{
    [ApiController]
    [Route("api/developer/auth")]
    [AllowAnonymous]
    public class DeveloperAuthController : ControllerBase
    {
        // Tenant "Sandbox" compartido (sembrado en la migración AddDeveloperPortal) bajo el que
        // cuelgan los Clients de prueba auto-provisionados a los developers que se registran de
        // forma independiente, sin invitación de ningún Tenant real.
        private static readonly Guid SandboxTenantId = Guid.Parse("00000000-0000-0000-0000-000000000900");

        // Mismo Integrator por defecto (DIAN directa) que usa TenantClientsController al crear
        // un Client nuevo — el Client de prueba se comporta exactamente como cualquier Client
        // recién creado, solo que en el Tenant Sandbox.
        private static readonly Guid DefaultIntegratorId = Guid.Parse("00000000-0000-0000-0000-000000000101");

        private readonly FelDbContext _dbContext;
        private readonly PasswordResetService _passwordResetService;
        private readonly ISessionTokenService _sessionTokenService;
        private readonly string _portalUrl;

        public DeveloperAuthController(FelDbContext dbContext, PasswordResetService passwordResetService, ISessionTokenService sessionTokenService, IConfiguration config)
        {
            _dbContext = dbContext;
            _passwordResetService = passwordResetService;
            _sessionTokenService = sessionTokenService;
            _portalUrl = config["DeveloperPortalUrl"] ?? "https://developers.facil-factura.pro";
        }

        private string IssueToken(Guid developerId) =>
            _sessionTokenService.GenerateToken(new[] { ("DeveloperId", developerId.ToString()) }, TimeSpan.FromHours(24));

        // Registro independiente: no requiere invitación ni Tenant. Se le auto-provisiona un
        // Client de prueba propio bajo el Tenant Sandbox para que tenga credenciales de prueba
        // (TestApiKey/TestApiSecret) de inmediato, sin depender de ningún Tenant real.
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] DeveloperRegisterRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            {
                return BadRequest("Nombre, correo y contraseña son obligatorios.");
            }

            if (await _dbContext.DeveloperUsers.AnyAsync(u => u.Email == request.Email))
            {
                return BadRequest("Ese correo ya está registrado.");
            }

            var sandboxClient = new Fel.Core.Entities.Client
            {
                Id = Guid.NewGuid(),
                TenantId = SandboxTenantId,
                CompanyName = $"Sandbox - {request.Name}",
                CommercialName = $"Sandbox - {request.Name}",
                Email = request.Email,
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

            var developer = new DeveloperUser
            {
                Id = Guid.NewGuid(),
                Name = request.Name,
                Email = request.Email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
                TenantId = null,
                ClientId = sandboxClient.Id,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };
            _dbContext.DeveloperUsers.Add(developer);

            await _dbContext.SaveChangesAsync();

            return Ok(new { token = IssueToken(developer.Id), developer.Id, developer.Name, developer.Email });
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] DeveloperLoginRequest request)
        {
            var user = await _dbContext.DeveloperUsers.FirstOrDefaultAsync(u => u.Email == request.Email);

            if (user == null || !user.IsActive)
            {
                return Unauthorized("Credenciales incorrectas o usuario inactivo.");
            }

            if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            {
                return Unauthorized("Credenciales incorrectas.");
            }

            return Ok(new
            {
                token = IssueToken(user.Id),
                user.Id,
                user.Name,
                user.Email,
                tenantId = user.TenantId,
                clientId = user.ClientId
            });
        }

        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] DeveloperForgotPasswordRequest request)
        {
            const string genericMessage = "Si el correo está registrado, te enviamos un enlace para restablecer tu contraseña.";

            var user = await _dbContext.DeveloperUsers.FirstOrDefaultAsync(u => u.Email == request.Email && u.IsActive);
            if (user == null) return Ok(new { message = genericMessage });

            await _passwordResetService.RequestAsync(
                PortalUserType.Developer, user.Id, user.Email, user.Name, _portalUrl, "password_reset");

            return Ok(new { message = genericMessage });
        }

        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] DeveloperResetPasswordRequest request)
        {
            var consumed = await _passwordResetService.ConsumeAsync(request.Token, PortalUserType.Developer);
            if (consumed == null) return BadRequest("El enlace no es válido o ya expiró. Solicita uno nuevo.");

            var user = await _dbContext.DeveloperUsers.FindAsync(consumed.Value.UserId);
            if (user == null) return BadRequest("El enlace no es válido o ya expiró. Solicita uno nuevo.");

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
            await _dbContext.SaveChangesAsync();

            return Ok(new { message = "Contraseña actualizada correctamente." });
        }
    }

    public class DeveloperRegisterRequest
    {
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public class DeveloperLoginRequest
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public class DeveloperForgotPasswordRequest
    {
        public string Email { get; set; } = string.Empty;
    }

    public class DeveloperResetPasswordRequest
    {
        public string Token { get; set; } = string.Empty;
        public string NewPassword { get; set; } = string.Empty;
    }
}
