using System;
using System.Threading.Tasks;
using Fel.Core.Interfaces;
using Fel.Infrastructure.Data;
using MailKit.Net.Imap;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Fel.Api.Client.Controllers
{
    /// <summary>
    /// Configuración de Eventos de Recepción RADIAN: conexión de correo de facturación electrónica
    /// y cuáles eventos se disparan automáticamente al recibir un documento.
    /// </summary>
    [ApiController]
    [Route("api/client/reception-settings")]
    public class ReceptionSettingsController : ControllerBase
    {
        private readonly FelDbContext _dbContext;
        private readonly ICryptoService _cryptoService;

        public ReceptionSettingsController(FelDbContext dbContext, ICryptoService cryptoService)
        {
            _dbContext = dbContext;
            _cryptoService = cryptoService;
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
        public async Task<IActionResult> GetSettings()
        {
            try
            {
                var clientId = GetCurrentClientId();
                var client = await _dbContext.Clients.FirstOrDefaultAsync(c => c.Id == clientId);
                if (client == null) return NotFound();

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
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(ex.Message);
            }
        }

        public class UpdateSettingsRequest
        {
            public bool ReceptionEmailEnabled { get; set; }
            public string ReceptionEmailHost { get; set; } = string.Empty;
            public int ReceptionEmailPort { get; set; } = 993;
            public bool ReceptionEmailUseSsl { get; set; } = true;
            public string ReceptionEmailUser { get; set; } = string.Empty;
            // Vacío = no cambiar la contraseña ya guardada.
            public string? ReceptionEmailPassword { get; set; }

            public bool AutoSendAcuseRecibo { get; set; }
            public bool AutoSendReciboBien { get; set; }
            public bool AutoSendAceptacion { get; set; }
            public bool AutoSendReclamo { get; set; }
        }

        [HttpPut]
        public async Task<IActionResult> UpdateSettings([FromBody] UpdateSettingsRequest request)
        {
            Guid clientId;
            try
            {
                clientId = GetCurrentClientId();
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(ex.Message);
            }

            var client = await _dbContext.Clients.FirstOrDefaultAsync(c => c.Id == clientId);
            if (client == null) return NotFound();

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

        /// <summary>
        /// Intenta conectarse y autenticarse contra el correo configurado, sin leer ni marcar nada,
        /// solo para confirmar que las credenciales funcionan.
        /// </summary>
        [HttpPost("test-connection")]
        public async Task<IActionResult> TestConnection()
        {
            Guid clientId;
            try
            {
                clientId = GetCurrentClientId();
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(ex.Message);
            }

            var client = await _dbContext.Clients.FirstOrDefaultAsync(c => c.Id == clientId);
            if (client == null) return NotFound();

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
