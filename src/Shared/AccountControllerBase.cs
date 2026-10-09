using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Fel.Core.Entities;
using Fel.Core.Interfaces;
using Fel.Core.Security;
using Fel.Infrastructure.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Fel.Api.Shared
{
    public record UpdateProfileRequest(string? Name);
    public record ChangePasswordRequest(string? CurrentPassword, string? NewPassword);
    public record SessionPreferenceRequest(int Minutes);
    public record ReauthenticateRequest(string? Password);

    // Perfil y sesión del usuario que inició sesión en un portal: ver y editar su nombre, cambiar la contraseña, elegir cuánto dura su sesión
    // por inactividad, renovarla y cerrarla en todos los dispositivos. Las cuatro APIs (tenant, cliente, developers y superadmin) lo comparten
    // y solo implementan cómo se carga la cuenta y cómo se guarda su contraseña. Este archivo se enlaza en cada proyecto de API.
    //
    // Los errores de contraseña salen como 400 y no como 401: un 401 hace que el portal cierre la sesión, y equivocarse al escribir la
    // contraseña no debe sacar a nadie.
    [ApiController]
    [Authorize]
    public abstract class AccountControllerBase : ControllerBase
    {
        protected AccountSessionService Sessions { get; }

        protected AccountControllerBase(AccountSessionService sessions) => Sessions = sessions;

        protected abstract PortalKind Kind { get; }
        protected abstract Task<ISessionAccount?> FindAccountAsync(ClaimsPrincipal user);
        protected abstract Task SaveAsync();
        protected abstract bool VerifyPassword(string password, string hash);
        protected abstract string HashPassword(string password);
        // Rol y organización que se muestran en el menú del avatar.
        protected abstract Task<(string Role, string? Organization)> DescribeAsync(ISessionAccount account);
        // La cuenta puede renovar solo si todavía tiene acceso vigente (por ejemplo, al tenant del token).
        protected virtual Task<bool> CanRenewAsync(ISessionAccount account, ClaimsPrincipal user) => Task.FromResult(true);

        private async Task<object> MeAsync(ISessionAccount account)
        {
            var (role, organization) = await DescribeAsync(account);
            return new
            {
                name = account.Name,
                displayName = AccountSessionService.DisplayName(account),
                email = account.Email,
                role,
                organization,
                sessionMinutes = SessionPolicy.Effective(Kind, account.SessionMinutes),
                allowedSessionMinutes = SessionPolicy.AllowedFor(Kind),
                absoluteCapHours = (int)SessionPolicy.AbsoluteCap.TotalHours
            };
        }

        // Contraseña equivocada en una operación con sesión abierta: 400, o 429 y cierre de todas las sesiones al llegar al límite de intentos.
        private async Task<IActionResult> PasswordFailedAsync(ISessionAccount account, string message)
        {
            if (!Sessions.RegisterPasswordFailure(Kind, account.Id)) return BadRequest(message);

            Sessions.RotateStamp(Kind, account);
            await SaveAsync();
            return StatusCode(StatusCodes.Status429TooManyRequests, "Demasiados intentos con la contraseña equivocada. Por seguridad cerramos tus sesiones: inicia sesión de nuevo.");
        }

        private static object TokenBody(SessionTokenResult result) => new { token = result.Token, expiresAt = result.ExpiresAtUtc };

        [HttpGet("me")]
        public async Task<IActionResult> Me()
        {
            var account = await FindAccountAsync(User);
            return account == null ? Unauthorized() : Ok(await MeAsync(account));
        }

        [HttpPut("me")]
        public async Task<IActionResult> UpdateMe([FromBody] UpdateProfileRequest request)
        {
            var account = await FindAccountAsync(User);
            if (account == null) return Unauthorized();

            var error = Sessions.SetName(account, request.Name);
            if (error != null) return BadRequest(error);

            await SaveAsync();
            return Ok(await MeAsync(account));
        }

        [HttpPost("change-password")]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
        {
            var account = await FindAccountAsync(User);
            if (account == null || !await CanRenewAsync(account, User)) return Unauthorized();

            var error = Sessions.ChangePassword(Kind, account, request.CurrentPassword, request.NewPassword, VerifyPassword, HashPassword);
            if (error == AccountSessionService.WrongCurrentPassword) return await PasswordFailedAsync(account, error);
            if (error != null) return BadRequest(error);

            await SaveAsync();
            // Las demás sesiones murieron con el cambio de sello; esta sigue con un token nuevo (y como acaba de confirmar su contraseña, el
            // tope absoluto vuelve a contar desde ahora).
            return Ok(TokenBody(Sessions.Reauthenticated(Kind, account, User)));
        }

        [HttpPut("session-preference")]
        public async Task<IActionResult> SetSessionPreference([FromBody] SessionPreferenceRequest request)
        {
            var account = await FindAccountAsync(User);
            if (account == null || !await CanRenewAsync(account, User)) return Unauthorized();

            var error = Sessions.SetSessionMinutes(Kind, account, request.Minutes);
            if (error != null) return BadRequest(error);

            await SaveAsync();
            // Con el token nuevo la duración elegida rige desde ya.
            var renewed = Sessions.Renew(Kind, account, User);
            return Ok(new { me = await MeAsync(account), token = renewed?.Token, expiresAt = renewed?.ExpiresAtUtc });
        }

        [HttpPost("renew")]
        public async Task<IActionResult> Renew()
        {
            var account = await FindAccountAsync(User);
            if (account == null || !await CanRenewAsync(account, User)) return Unauthorized();

            var renewed = Sessions.Renew(Kind, account, User);
            // 409 y no 401: la sesión llegó al tope de 12 horas y hay que confirmar la contraseña, no volver a empezar.
            return renewed == null ? Conflict(new { reauthRequired = true }) : Ok(TokenBody(renewed));
        }

        [HttpPost("reauthenticate")]
        public async Task<IActionResult> Reauthenticate([FromBody] ReauthenticateRequest request)
        {
            var account = await FindAccountAsync(User);
            if (account == null || !await CanRenewAsync(account, User)) return Unauthorized();
            if (string.IsNullOrEmpty(request.Password) || !VerifyPassword(request.Password, account.PasswordHash)) return await PasswordFailedAsync(account, "La contraseña no es correcta.");

            Sessions.ClearPasswordFailures(Kind, account.Id);

            return Ok(TokenBody(Sessions.Reauthenticated(Kind, account, User)));
        }

        [HttpPost("sign-out-everywhere")]
        public async Task<IActionResult> SignOutEverywhere()
        {
            var account = await FindAccountAsync(User);
            if (account == null) return Unauthorized();
            if (Sessions.Renew(Kind, account, User) == null) return Conflict(new { reauthRequired = true });

            Sessions.RotateStamp(Kind, account);
            await SaveAsync();
            // Todas las sesiones anteriores quedan sin efecto; esta sigue con el sello nuevo.
            return Ok(TokenBody(Sessions.Renew(Kind, account, User)!));
        }
    }
}
