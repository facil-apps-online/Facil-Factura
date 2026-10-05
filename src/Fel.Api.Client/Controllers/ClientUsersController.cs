using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Fel.Api.Security;
using Fel.Core.Entities;
using Fel.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace Fel.Api.Client.Controllers
{
    // Usuarios del portal del Client: los gestiona su Administrador. Las reglas viven en ClientUserAdminService (las comparte tenant-web).
    [ApiController]
    [Route("api/client/users")]
    [ClientRole(ClientUserRoles.Administrator)]
    [AllowAllBranches]
    public class ClientUsersController : ClientPortalControllerBase
    {
        private readonly ClientUserAdminService _users;
        private readonly string _portalUrl;

        public ClientUsersController(ClientUserAdminService users, IConfiguration config)
        {
            _users = users;
            _portalUrl = config["PortalUrl"] ?? "https://clients.facil-factura.pro";
        }

        public class SaveClientUserRequest
        {
            public string Name { get; set; } = string.Empty;
            public string Email { get; set; } = string.Empty;
            public string Role { get; set; } = string.Empty;
            public bool AllBranches { get; set; }
            public List<Guid>? BranchIds { get; set; }
        }

        private static ClientUserAdminService.SaveInput ToInput(SaveClientUserRequest r) =>
            new(r.Name ?? string.Empty, r.Email ?? string.Empty, r.Role ?? string.Empty, r.AllBranches, r.BranchIds);

        private IActionResult Failure<T>(UserAdminResult<T> result) =>
            result.NotFound ? NotFound() : BadRequest(result.Error);

        [HttpGet]
        public async Task<IActionResult> List() =>
            Ok(await _users.ListAsync(GetCurrentClientId(), Branch.UserId));

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] SaveClientUserRequest request)
        {
            var result = await _users.CreateAsync(GetCurrentClientId(), ToInput(request), _portalUrl);
            if (!result.Succeeded) return Failure(result);

            var (user, invitation) = result.Value;
            return Ok(new { user.Id, user.Name, user.Email, user.Role, user.AllBranches, user.IsActive, invitationSent = invitation.Sent, invitationError = invitation.Error });
        }

        [HttpPut("{id:guid}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] SaveClientUserRequest request)
        {
            var result = await _users.UpdateAsync(GetCurrentClientId(), id, ToInput(request), Branch.UserId);
            if (!result.Succeeded) return Failure(result);

            var user = result.Value!;
            return Ok(new { user.Id, user.Name, user.Email, user.Role, user.AllBranches, user.IsActive });
        }

        [HttpPost("{id:guid}/resend-invitation")]
        public async Task<IActionResult> ResendInvitation(Guid id)
        {
            var result = await _users.ResendInvitationAsync(GetCurrentClientId(), id, _portalUrl);
            if (!result.Succeeded) return Failure(result);

            var invitation = result.Value!;
            return Ok(invitation.Sent
                ? new { message = "Invitación reenviada.", sent = true, detail = (string?)null }
                : new { message = "No se pudo enviar el correo de invitación.", sent = false, detail = invitation.Error });
        }

        [HttpPost("{id:guid}/deactivate")]
        public async Task<IActionResult> Deactivate(Guid id)
        {
            var result = await _users.SetActiveAsync(GetCurrentClientId(), id, active: false, Branch.UserId);
            return result.Succeeded ? Ok(new { result.Value!.Id, result.Value.IsActive }) : Failure(result);
        }

        [HttpPost("{id:guid}/reactivate")]
        public async Task<IActionResult> Reactivate(Guid id)
        {
            var result = await _users.SetActiveAsync(GetCurrentClientId(), id, active: true, Branch.UserId);
            return result.Succeeded ? Ok(new { result.Value!.Id, result.Value.IsActive }) : Failure(result);
        }
    }
}
