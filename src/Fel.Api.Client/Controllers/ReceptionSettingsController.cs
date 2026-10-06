using System;
using System.Threading.Tasks;
using Fel.Core.Interfaces;
using Fel.Infrastructure.Data;
using Fel.Infrastructure.Services;
using MailKit.Net.Imap;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Fel.Api.Security;
using Fel.Core.Entities;

namespace Fel.Api.Client.Controllers
{
    /// <summary>
    /// Configuración de Eventos de Recepción RADIAN: conexión de correo de facturación electrónica
    /// y cuáles eventos se disparan automáticamente al recibir un documento.
    ///
    /// El buzón depende de la sucursal elegida en el portal: con "Todas" se edita el del Client (el valor por defecto, y lo que llega a
    /// él queda en la sucursal principal); con una sucursal concreta se edita el propio de esa sucursal, que reemplaza al del Client y
    /// se puede quitar para volver a heredarlo. Los eventos automáticos son del Client.
    /// </summary>
    [ApiController]
    [Route("api/client/reception-settings")]
    [ClientRole(ClientUserRoles.Administrator)]
    [AllowAllBranches]
    public class ReceptionSettingsController : ClientPortalControllerBase
    {
        private readonly FelDbContext _dbContext;
        private readonly ICryptoService _cryptoService;
        private readonly BranchCredentialResolver _resolver;

        public ReceptionSettingsController(FelDbContext dbContext, ICryptoService cryptoService, BranchCredentialResolver resolver)
        {
            _dbContext = dbContext;
            _cryptoService = cryptoService;
            _resolver = resolver;
        }

        private static object ToDto(ReceptionMailbox mailbox, Fel.Core.Entities.Client client, Guid? branchScope) => new
        {
            // "client" = el buzón por defecto del Client; "branch" = el de la sucursal elegida (propio o heredado, ver isBranchOwn).
            scope = branchScope.HasValue ? "branch" : "client",
            isBranchOwn = mailbox.IsBranchOwn,
            ReceptionEmailEnabled = mailbox.Enabled,
            ReceptionEmailHost = mailbox.Host,
            ReceptionEmailPort = mailbox.Port,
            ReceptionEmailUseSsl = mailbox.UseSsl,
            ReceptionEmailUser = mailbox.User,
            HasPassword = !string.IsNullOrEmpty(mailbox.PasswordEncrypted),
            client.AutoSendAcuseRecibo,
            client.AutoSendReciboBien,
            client.AutoSendAceptacion,
            client.AutoSendReclamo
        };

        private async Task<ReceptionMailbox> CurrentMailboxAsync(Fel.Core.Entities.Client client, Guid? branchScope) =>
            branchScope.HasValue
                ? await _resolver.ReceptionMailboxAsync(client, branchScope.Value)
                : BranchCredentialResolver.FromClientMailbox(client, await BranchProvisioning.MainBranchIdAsync(_dbContext, client.Id));

        [HttpGet]
        public async Task<IActionResult> GetSettings()
        {
            try
            {
                var clientId = GetCurrentClientId();
                var client = await _dbContext.Clients.AsNoTracking().FirstOrDefaultAsync(c => c.Id == clientId);
                if (client == null) return NotFound();

                return Ok(ToDto(await CurrentMailboxAsync(client, CurrentBranchScope), client, CurrentBranchScope));
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

            // true = guardar solo los eventos automáticos (del cliente) sin tocar ningún buzón: así guardarlos desde una sucursal no le crea
            // un buzón propio con los valores heredados.
            public bool OnlyAutoSend { get; set; }

            public bool AutoSendAcuseRecibo { get; set; }
            public bool AutoSendReciboBien { get; set; }
            public bool AutoSendAceptacion { get; set; }
            public bool AutoSendReclamo { get; set; }
        }

        [HttpPut]
        public async Task<IActionResult> UpdateSettings([FromBody] UpdateSettingsRequest request)
        {
            Guid clientId;
            Guid? branchScope;
            try
            {
                clientId = GetCurrentClientId();
                branchScope = CurrentBranchScope;
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(ex.Message);
            }

            var client = await _dbContext.Clients.FirstOrDefaultAsync(c => c.Id == clientId);
            if (client == null) return NotFound();

            if (!request.OnlyAutoSend && request.ReceptionEmailEnabled && (string.IsNullOrWhiteSpace(request.ReceptionEmailHost) || string.IsNullOrWhiteSpace(request.ReceptionEmailUser)))
                return BadRequest("Para habilitar el buzón indica el host y el usuario.");
            if (!request.OnlyAutoSend && (request.ReceptionEmailPort < 1 || request.ReceptionEmailPort > 65535))
                return BadRequest("El puerto no es válido.");

            if (request.OnlyAutoSend)
            {
                // Solo los eventos automáticos: se guardan abajo en el Client.
            }
            else if (branchScope.HasValue)
            {
                // La sucursal debe ser del Client (el filtro del portal ya lo garantiza, pero esta fila se escribe por BranchId).
                if (!await _dbContext.Branches.AnyAsync(b => b.Id == branchScope.Value && b.ClientId == clientId)) return NotFound();

                var row = await _dbContext.BranchReceptionMailboxes.FirstOrDefaultAsync(x => x.BranchId == branchScope.Value);
                if (row == null)
                {
                    row = new BranchReceptionMailbox { BranchId = branchScope.Value };
                    _dbContext.BranchReceptionMailboxes.Add(row);
                }
                row.Enabled = request.ReceptionEmailEnabled;
                row.Host = request.ReceptionEmailHost?.Trim() ?? string.Empty;
                row.Port = request.ReceptionEmailPort;
                row.UseSsl = request.ReceptionEmailUseSsl;
                row.User = request.ReceptionEmailUser?.Trim() ?? string.Empty;
                if (!string.IsNullOrEmpty(request.ReceptionEmailPassword))
                {
                    row.PasswordEncrypted = _cryptoService.Encrypt(request.ReceptionEmailPassword);
                }
            }
            else
            {
                client.ReceptionEmailEnabled = request.ReceptionEmailEnabled;
                client.ReceptionEmailHost = request.ReceptionEmailHost;
                client.ReceptionEmailPort = request.ReceptionEmailPort;
                client.ReceptionEmailUseSsl = request.ReceptionEmailUseSsl;
                client.ReceptionEmailUser = request.ReceptionEmailUser;
                if (!string.IsNullOrEmpty(request.ReceptionEmailPassword))
                {
                    client.ReceptionEmailPasswordEncrypted = _cryptoService.Encrypt(request.ReceptionEmailPassword);
                }
            }

            client.AutoSendAcuseRecibo = request.AutoSendAcuseRecibo;
            client.AutoSendReciboBien = request.AutoSendReciboBien;
            client.AutoSendAceptacion = request.AutoSendAceptacion;
            client.AutoSendReclamo = request.AutoSendReclamo;

            await _dbContext.SaveChangesAsync();
            return Ok(new { message = "Configuración guardada." });
        }

        /// <summary>
        /// Quita el buzón propio de la sucursal elegida: vuelve a usar el del Client. Solo con una sucursal concreta elegida.
        /// </summary>
        [HttpDelete]
        public async Task<IActionResult> RemoveBranchMailbox()
        {
            Guid clientId;
            Guid? branchScope;
            try
            {
                clientId = GetCurrentClientId();
                branchScope = CurrentBranchScope;
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(ex.Message);
            }

            if (!branchScope.HasValue) return BadRequest("Elige una sucursal para quitar su buzón propio.");

            var row = await _dbContext.BranchReceptionMailboxes.FirstOrDefaultAsync(x => x.BranchId == branchScope.Value && x.Branch.ClientId == clientId);
            if (row != null)
            {
                _dbContext.BranchReceptionMailboxes.Remove(row);
                await _dbContext.SaveChangesAsync();
            }
            return Ok(new { message = "La sucursal vuelve a usar el buzón del cliente." });
        }

        /// <summary>
        /// Intenta conectarse y autenticarse contra el correo configurado, sin leer ni marcar nada,
        /// solo para confirmar que las credenciales funcionan.
        /// </summary>
        [HttpPost("test-connection")]
        public async Task<IActionResult> TestConnection()
        {
            Guid clientId;
            Guid? branchScope;
            try
            {
                clientId = GetCurrentClientId();
                branchScope = CurrentBranchScope;
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(ex.Message);
            }

            var client = await _dbContext.Clients.AsNoTracking().FirstOrDefaultAsync(c => c.Id == clientId);
            if (client == null) return NotFound();

            var mailbox = await CurrentMailboxAsync(client, branchScope);
            if (string.IsNullOrWhiteSpace(mailbox.Host) || string.IsNullOrWhiteSpace(mailbox.PasswordEncrypted))
            {
                return BadRequest(new { message = "Guarda primero el host, usuario y contraseña." });
            }

            try
            {
                using var imap = new ImapClient();
                await imap.ConnectAsync(mailbox.Host, mailbox.Port, mailbox.UseSsl);
                var password = _cryptoService.Decrypt(mailbox.PasswordEncrypted);
                await imap.AuthenticateAsync(mailbox.User, password);
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
