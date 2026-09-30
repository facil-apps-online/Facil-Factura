using System;
using System.Threading.Tasks;
using Fel.Core.Interfaces;
using Fel.Infrastructure.Data;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Fel.Api.Tenant.Controllers
{
    /// <summary>
    /// SMTP propio del Tenant: transporte de reenvío para los Clients del flujo nativo DIAN que no
    /// configuraron su propio SMTP (ver IEmailSender: Client > Tenant > plataforma).
    /// </summary>
    [ApiController]
    [Route("api/tenant/smtp-settings")]
    public class TenantSmtpSettingsController : ControllerBase
    {
        private readonly FelDbContext _dbContext;
        private readonly ICryptoService _cryptoService;

        public TenantSmtpSettingsController(FelDbContext dbContext, ICryptoService cryptoService)
        {
            _dbContext = dbContext;
            _cryptoService = cryptoService;
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
        public async Task<IActionResult> GetSettings()
        {
            var tenantId = GetCurrentTenantId();
            var tenant = await _dbContext.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId);
            if (tenant == null) return NotFound();

            return Ok(new
            {
                tenant.SmtpHost,
                tenant.SmtpPort,
                tenant.SmtpUseSsl,
                tenant.SmtpUser,
                tenant.SmtpFromEmail,
                tenant.SmtpFromName,
                HasPassword = !string.IsNullOrEmpty(tenant.SmtpPasswordEncrypted)
            });
        }

        public class UpdateSettingsRequest
        {
            public string? SmtpHost { get; set; }
            public int? SmtpPort { get; set; }
            public bool SmtpUseSsl { get; set; } = true;
            public string? SmtpUser { get; set; }
            public string? SmtpPassword { get; set; }
            public string? SmtpFromEmail { get; set; }
            public string? SmtpFromName { get; set; }
        }

        [HttpPut]
        public async Task<IActionResult> UpdateSettings([FromBody] UpdateSettingsRequest request)
        {
            var tenantId = GetCurrentTenantId();
            var tenant = await _dbContext.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId);
            if (tenant == null) return NotFound();

            tenant.SmtpHost = request.SmtpHost;
            tenant.SmtpPort = request.SmtpPort;
            tenant.SmtpUseSsl = request.SmtpUseSsl;
            tenant.SmtpUser = request.SmtpUser;
            tenant.SmtpFromEmail = request.SmtpFromEmail;
            tenant.SmtpFromName = request.SmtpFromName;
            if (!string.IsNullOrEmpty(request.SmtpPassword))
            {
                tenant.SmtpPasswordEncrypted = _cryptoService.Encrypt(request.SmtpPassword);
            }

            await _dbContext.SaveChangesAsync();
            return Ok(new { message = "Configuración guardada." });
        }

        [HttpPost("test-connection")]
        public async Task<IActionResult> TestConnection()
        {
            var tenantId = GetCurrentTenantId();
            var tenant = await _dbContext.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId);
            if (tenant == null) return NotFound();

            if (string.IsNullOrWhiteSpace(tenant.SmtpHost) || string.IsNullOrWhiteSpace(tenant.SmtpPasswordEncrypted))
            {
                return BadRequest(new { message = "Guarda primero el host, usuario y contraseña." });
            }

            try
            {
                using var smtp = new SmtpClient();
                var socketOptions = tenant.SmtpUseSsl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTlsWhenAvailable;
                await smtp.ConnectAsync(tenant.SmtpHost, tenant.SmtpPort ?? 587, socketOptions);
                var password = _cryptoService.Decrypt(tenant.SmtpPasswordEncrypted);
                await smtp.AuthenticateAsync(tenant.SmtpUser, password);
                await smtp.DisconnectAsync(true);
                return Ok(new { success = true, message = "Conexión exitosa." });
            }
            catch (Exception ex)
            {
                return Ok(new { success = false, message = $"No se pudo conectar: {ex.Message}" });
            }
        }
    }
}
