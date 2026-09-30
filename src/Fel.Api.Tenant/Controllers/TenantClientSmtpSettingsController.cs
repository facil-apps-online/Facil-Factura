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
    /// Permite al tenant configurar, en nombre de su cliente, el SMTP propio para reenvío de
    /// documentos del flujo nativo DIAN — mismo contrato que <c>api/client/smtp-settings</c>. El
    /// tenant nunca entra al portal del cliente; ambos editan el mismo campo desde su propio lado.
    /// </summary>
    [ApiController]
    [Route("api/tenant/clients/{clientId}/smtp-settings")]
    public class TenantClientSmtpSettingsController : ControllerBase
    {
        private readonly FelDbContext _dbContext;
        private readonly ICryptoService _cryptoService;

        public TenantClientSmtpSettingsController(FelDbContext dbContext, ICryptoService cryptoService)
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
        public async Task<IActionResult> GetSettings(Guid clientId)
        {
            var tenantId = GetCurrentTenantId();
            var client = await _dbContext.Clients.FirstOrDefaultAsync(c => c.Id == clientId && c.TenantId == tenantId);
            if (client == null) return StatusCode(StatusCodes.Status403Forbidden);

            return Ok(new
            {
                client.SmtpHost,
                client.SmtpPort,
                client.SmtpUseSsl,
                client.SmtpUser,
                client.SmtpFromEmail,
                client.SmtpFromName,
                HasPassword = !string.IsNullOrEmpty(client.SmtpPasswordEncrypted)
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
        public async Task<IActionResult> UpdateSettings(Guid clientId, [FromBody] UpdateSettingsRequest request)
        {
            var tenantId = GetCurrentTenantId();
            var client = await _dbContext.Clients.FirstOrDefaultAsync(c => c.Id == clientId && c.TenantId == tenantId);
            if (client == null) return StatusCode(StatusCodes.Status403Forbidden);

            client.SmtpHost = request.SmtpHost;
            client.SmtpPort = request.SmtpPort;
            client.SmtpUseSsl = request.SmtpUseSsl;
            client.SmtpUser = request.SmtpUser;
            client.SmtpFromEmail = request.SmtpFromEmail;
            client.SmtpFromName = request.SmtpFromName;
            if (!string.IsNullOrEmpty(request.SmtpPassword))
            {
                client.SmtpPasswordEncrypted = _cryptoService.Encrypt(request.SmtpPassword);
            }

            await _dbContext.SaveChangesAsync();
            return Ok(new { message = "Configuración guardada." });
        }

        [HttpPost("test-connection")]
        public async Task<IActionResult> TestConnection(Guid clientId)
        {
            var tenantId = GetCurrentTenantId();
            var client = await _dbContext.Clients.FirstOrDefaultAsync(c => c.Id == clientId && c.TenantId == tenantId);
            if (client == null) return StatusCode(StatusCodes.Status403Forbidden);

            if (string.IsNullOrWhiteSpace(client.SmtpHost) || string.IsNullOrWhiteSpace(client.SmtpPasswordEncrypted))
            {
                return BadRequest(new { message = "Guarda primero el host, usuario y contraseña." });
            }

            try
            {
                using var smtp = new SmtpClient();
                var socketOptions = client.SmtpUseSsl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTlsWhenAvailable;
                await smtp.ConnectAsync(client.SmtpHost, client.SmtpPort ?? 587, socketOptions);
                var password = _cryptoService.Decrypt(client.SmtpPasswordEncrypted);
                await smtp.AuthenticateAsync(client.SmtpUser, password);
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
