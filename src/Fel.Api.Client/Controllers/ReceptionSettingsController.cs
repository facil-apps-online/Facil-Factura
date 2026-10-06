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
    /// Todo es de la sucursal elegida en el portal (con "Todas" se trabaja sobre la principal, que es el valor por defecto de las demás):
    /// una sucursal sin buzón o sin eventos propios usa los de la principal; al guardar los suyos, éstos los reemplazan, y DELETE los quita
    /// para volver a heredar (la principal no se quita: se edita).
    /// </summary>
    [ApiController]
    [Route("api/client/reception-settings")]
    [ClientRole(ClientUserRoles.Administrator)]
    [AllowAllBranches]
    public class ReceptionSettingsController : ClientPortalControllerBase
    {
        private const string MainIsDefault = "La sucursal principal es el valor por defecto de las demás: edítala en lugar de quitarla.";

        private readonly FelDbContext _dbContext;
        private readonly ICryptoService _cryptoService;
        private readonly BranchCredentialResolver _resolver;

        public ReceptionSettingsController(FelDbContext dbContext, ICryptoService cryptoService, BranchCredentialResolver resolver)
        {
            _dbContext = dbContext;
            _cryptoService = cryptoService;
            _resolver = resolver;
        }

        // Sucursal sobre la que se trabaja: la elegida o, con "Todas", la principal.
        private async Task<(Guid ClientId, Guid BranchId, bool IsMain)> TargetAsync()
        {
            var clientId = GetCurrentClientId();
            var mainId = await BranchProvisioning.MainBranchIdAsync(_dbContext, clientId);
            var branchId = CurrentBranchScope ?? mainId;
            return (clientId, branchId, branchId == mainId);
        }

        [HttpGet]
        public async Task<IActionResult> GetSettings()
        {
            try
            {
                var (clientId, branchId, isMain) = await TargetAsync();
                var mailbox = await _resolver.ReceptionMailboxAsync(clientId, branchId);
                var events = await _resolver.ReceptionEventsAsync(clientId, branchId);

                return Ok(new
                {
                    isMain,
                    // Con propios = lo guardado es de esta sucursal; sin propios = se está viendo lo heredado de la principal.
                    isBranchOwn = mailbox.IsBranchOwn,
                    isEventsOwn = events.IsBranchOwn,
                    ReceptionEmailEnabled = mailbox.Enabled,
                    ReceptionEmailHost = mailbox.Host,
                    ReceptionEmailPort = mailbox.Port,
                    ReceptionEmailUseSsl = mailbox.UseSsl,
                    ReceptionEmailUser = mailbox.User,
                    HasPassword = !string.IsNullOrEmpty(mailbox.PasswordEncrypted),
                    AutoSendAcuseRecibo = events.AcuseRecibo,
                    AutoSendReciboBien = events.ReciboBien,
                    AutoSendAceptacion = events.Aceptacion,
                    AutoSendReclamo = events.Reclamo
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

            // true = guardar solo los eventos automáticos sin tocar el buzón; false = guardar solo el buzón. Son dos tarjetas distintas y
            // guardar una no debe crear (ni cambiar) lo de la otra.
            public bool OnlyAutoSend { get; set; }

            public bool AutoSendAcuseRecibo { get; set; }
            public bool AutoSendReciboBien { get; set; }
            public bool AutoSendAceptacion { get; set; }
            public bool AutoSendReclamo { get; set; }
        }

        [HttpPut]
        public async Task<IActionResult> UpdateSettings([FromBody] UpdateSettingsRequest request)
        {
            Guid clientId, branchId;
            try
            {
                (clientId, branchId, _) = await TargetAsync();
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(ex.Message);
            }

            if (request.OnlyAutoSend)
            {
                var events = await _dbContext.BranchReceptionEvents.FirstOrDefaultAsync(x => x.BranchId == branchId);
                if (events == null)
                {
                    events = new BranchReceptionEvents { BranchId = branchId };
                    _dbContext.BranchReceptionEvents.Add(events);
                }
                events.AutoSendAcuseRecibo = request.AutoSendAcuseRecibo;
                events.AutoSendReciboBien = request.AutoSendReciboBien;
                events.AutoSendAceptacion = request.AutoSendAceptacion;
                events.AutoSendReclamo = request.AutoSendReclamo;
                await _dbContext.SaveChangesAsync();
                return Ok(new { message = "Configuración guardada." });
            }

            if (request.ReceptionEmailEnabled && (string.IsNullOrWhiteSpace(request.ReceptionEmailHost) || string.IsNullOrWhiteSpace(request.ReceptionEmailUser)))
                return BadRequest("Para habilitar el buzón indica el host y el usuario.");
            if (request.ReceptionEmailPort < 1 || request.ReceptionEmailPort > 65535)
                return BadRequest("El puerto no es válido.");

            var row = await _dbContext.BranchReceptionMailboxes.FirstOrDefaultAsync(x => x.BranchId == branchId && x.Branch.ClientId == clientId);
            if (row == null)
            {
                row = new BranchReceptionMailbox { BranchId = branchId };
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

            await _dbContext.SaveChangesAsync();
            return Ok(new { message = "Configuración guardada." });
        }

        /// <summary>Quita el buzón propio de la sucursal elegida: vuelve a usar el de la principal.</summary>
        [HttpDelete]
        public async Task<IActionResult> RemoveBranchMailbox()
        {
            Guid clientId, branchId;
            bool isMain;
            try
            {
                (clientId, branchId, isMain) = await TargetAsync();
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(ex.Message);
            }
            if (isMain) return BadRequest(MainIsDefault);

            var row = await _dbContext.BranchReceptionMailboxes.FirstOrDefaultAsync(x => x.BranchId == branchId && x.Branch.ClientId == clientId);
            if (row != null)
            {
                _dbContext.BranchReceptionMailboxes.Remove(row);
                await _dbContext.SaveChangesAsync();
            }
            return Ok(new { message = "La sucursal vuelve a usar el buzón de la principal." });
        }

        /// <summary>Quita los eventos automáticos propios de la sucursal elegida: vuelve a usar los de la principal.</summary>
        [HttpDelete("events")]
        public async Task<IActionResult> RemoveBranchEvents()
        {
            Guid clientId, branchId;
            bool isMain;
            try
            {
                (clientId, branchId, isMain) = await TargetAsync();
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(ex.Message);
            }
            if (isMain) return BadRequest(MainIsDefault);

            var row = await _dbContext.BranchReceptionEvents.FirstOrDefaultAsync(x => x.BranchId == branchId && x.Branch.ClientId == clientId);
            if (row != null)
            {
                _dbContext.BranchReceptionEvents.Remove(row);
                await _dbContext.SaveChangesAsync();
            }
            return Ok(new { message = "La sucursal vuelve a usar los eventos de la principal." });
        }

        /// <summary>
        /// Intenta conectarse y autenticarse contra el correo configurado, sin leer ni marcar nada,
        /// solo para confirmar que las credenciales funcionan.
        /// </summary>
        [HttpPost("test-connection")]
        public async Task<IActionResult> TestConnection()
        {
            Guid clientId, branchId;
            try
            {
                (clientId, branchId, _) = await TargetAsync();
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(ex.Message);
            }

            var mailbox = await _resolver.ReceptionMailboxAsync(clientId, branchId);
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
