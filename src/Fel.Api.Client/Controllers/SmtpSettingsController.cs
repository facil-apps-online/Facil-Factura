using System;
using System.Threading.Tasks;
using Fel.Core.Interfaces;
using Fel.Infrastructure.Data;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Fel.Api.Client.Controllers
{
    /// <summary>
    /// SMTP propio del Client, usado como transporte preferente al reenviar documentos del flujo
    /// nativo DIAN (ver InvoiceController.Resend e IEmailSender: Client > Tenant > plataforma).
    /// El Tenant puede configurar este mismo campo en nombre del Client desde tenant-web
    /// (TenantClientsController.UpdateSmtpConfig) — es el mismo dato, editable desde cualquiera
    /// de los dos portales.
    /// </summary>
    [ApiController]
    [Route("api/client/smtp-settings")]
    public class SmtpSettingsController : ControllerBase
    {
        private readonly FelDbContext _dbContext;
        private readonly ICryptoService _cryptoService;

        public SmtpSettingsController(FelDbContext dbContext, ICryptoService cryptoService)
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
                    client.SmtpHost,
                    client.SmtpPort,
                    client.SmtpUseSsl,
                    client.SmtpUser,
                    client.SmtpFromEmail,
                    client.SmtpFromName,
                    HasPassword = !string.IsNullOrEmpty(client.SmtpPasswordEncrypted)
                });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(ex.Message);
            }
        }

        public class UpdateSettingsRequest
        {
            public string? SmtpHost { get; set; }
            public int? SmtpPort { get; set; }
            public bool SmtpUseSsl { get; set; } = true;
            public string? SmtpUser { get; set; }
            // Vacío = no cambiar la contraseña ya guardada.
            public string? SmtpPassword { get; set; }
            public string? SmtpFromEmail { get; set; }
            public string? SmtpFromName { get; set; }
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

        /// <summary>
        /// Intenta autenticarse contra el SMTP configurado sin enviar nada, solo para confirmar
        /// que las credenciales funcionan.
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
