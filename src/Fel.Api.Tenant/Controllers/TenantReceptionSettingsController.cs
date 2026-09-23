using System;
using System.Threading.Tasks;
using Fel.Core.Interfaces;
using Fel.Infrastructure.Data;
using MailKit.Net.Imap;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Fel.Api.Tenant.Controllers
{
    /// <summary>
    /// Permite al tenant configurar, en nombre de su cliente, la conexión de correo de recepción y
    /// los eventos RADIAN automáticos — mismo contrato que <c>api/client/reception-settings</c>.
    /// </summary>
    [ApiController]
    [Route("api/tenant/clients/{clientId}/reception-settings")]
    public class TenantReceptionSettingsController : ControllerBase
    {
        private readonly FelDbContext _dbContext;
        private readonly ICryptoService _cryptoService;

        public TenantReceptionSettingsController(FelDbContext dbContext, ICryptoService cryptoService)
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
                client.ReceptionEmailEnabled,
                client.ReceptionEmailHost,
                client.ReceptionEmailPort,
                client.ReceptionEmailUseSsl,
                client.ReceptionEmailUser,
                HasPassword = !string.IsNullOrEmpty(client.ReceptionEmailPasswordEncrypted),
                client.AutoSendAcuseRecibo,
                client.AutoSendReciboBien,
                client.AutoSendAceptacion,
                client.AutoSendReclamo
            });
        }

        public class UpdateSettingsRequest
        {
            public bool ReceptionEmailEnabled { get; set; }
            public string ReceptionEmailHost { get; set; } = string.Empty;
            public int ReceptionEmailPort { get; set; } = 993;
            public bool ReceptionEmailUseSsl { get; set; } = true;
            public string ReceptionEmailUser { get; set; } = string.Empty;
            public string? ReceptionEmailPassword { get; set; }

            public bool AutoSendAcuseRecibo { get; set; }
            public bool AutoSendReciboBien { get; set; }
            public bool AutoSendAceptacion { get; set; }
            public bool AutoSendReclamo { get; set; }
        }

        [HttpPut]
        public async Task<IActionResult> UpdateSettings(Guid clientId, [FromBody] UpdateSettingsRequest request)
        {
            var tenantId = GetCurrentTenantId();
            var client = await _dbContext.Clients.FirstOrDefaultAsync(c => c.Id == clientId && c.TenantId == tenantId);
            if (client == null) return StatusCode(StatusCodes.Status403Forbidden);

            client.ReceptionEmailEnabled = request.ReceptionEmailEnabled;
            client.ReceptionEmailHost = request.ReceptionEmailHost;
            client.ReceptionEmailPort = request.ReceptionEmailPort;
            client.ReceptionEmailUseSsl = request.ReceptionEmailUseSsl;
            client.ReceptionEmailUser = request.ReceptionEmailUser;
            if (!string.IsNullOrEmpty(request.ReceptionEmailPassword))
            {
                client.ReceptionEmailPasswordEncrypted = _cryptoService.Encrypt(request.ReceptionEmailPassword);
            }

            client.AutoSendAcuseRecibo = request.AutoSendAcuseRecibo;
            client.AutoSendReciboBien = request.AutoSendReciboBien;
            client.AutoSendAceptacion = request.AutoSendAceptacion;
            client.AutoSendReclamo = request.AutoSendReclamo;

            await _dbContext.SaveChangesAsync();
            return Ok(new { message = "Configuración guardada." });
        }

        [HttpPost("test-connection")]
        public async Task<IActionResult> TestConnection(Guid clientId)
        {
            var tenantId = GetCurrentTenantId();
            var client = await _dbContext.Clients.FirstOrDefaultAsync(c => c.Id == clientId && c.TenantId == tenantId);
            if (client == null) return StatusCode(StatusCodes.Status403Forbidden);

            if (string.IsNullOrWhiteSpace(client.ReceptionEmailHost) || string.IsNullOrWhiteSpace(client.ReceptionEmailPasswordEncrypted))
            {
                return BadRequest(new { message = "Guarda primero el host, usuario y contraseña." });
            }

            try
            {
                using var imap = new ImapClient();
                await imap.ConnectAsync(client.ReceptionEmailHost, client.ReceptionEmailPort, client.ReceptionEmailUseSsl);
                var password = _cryptoService.Decrypt(client.ReceptionEmailPasswordEncrypted);
                await imap.AuthenticateAsync(client.ReceptionEmailUser, password);
                await imap.DisconnectAsync(true);
                return Ok(new { success = true, message = "Conexión exitosa." });
            }
            catch (Exception ex)
            {
                return Ok(new { success = false, message = $"No se pudo conectar: {ex.Message}" });
            }
        }
    }
}
