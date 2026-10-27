using System;
using System.Linq;
using System.Threading.Tasks;
using Fel.Core.Entities;
using Fel.Infrastructure.Data;
using Fel.Infrastructure.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Fel.Api.Tenant.Controllers
{
    // Numeraciones de notas crédito y débito de un cliente del tenant: el tenant las define por él, igual que el administrador del cliente en su portal.
    // Cada cliente puede tener varias por tipo, con el prefijo que quiera, y una predeterminada; sin ninguna propia las notas llevan el prefijo de su factura.
    [ApiController]
    [Route("api/tenant/clients/{clientId:guid}/note-numberings")]
    public class TenantNoteNumberingsController : ControllerBase
    {
        private readonly FelDbContext _dbContext;
        private readonly NoteNumberingAdminService _numberings;

        public TenantNoteNumberingsController(FelDbContext dbContext, NoteNumberingAdminService numberings)
        {
            _dbContext = dbContext;
            _numberings = numberings;
        }

        public class SaveOwnNumberingRequest
        {
            public string Kind { get; set; } = string.Empty;
            public string Prefix { get; set; } = string.Empty;
            public long? NextNumber { get; set; }
        }

        // Sin x-tenant-id: 401; con un cliente que no es del tenant: 403, sin revelar nada del cliente. Null = puede seguir.
        private async Task<IActionResult?> GuardAsync(Guid clientId)
        {
            if (!Request.Headers.TryGetValue("x-tenant-id", out var tenantIdStr) || !Guid.TryParse(tenantIdStr, out var tenantId))
                return Unauthorized("x-tenant-id Header is missing");
            return await _dbContext.Clients.AnyAsync(c => c.Id == clientId && c.TenantId == tenantId) ? null : StatusCode(StatusCodes.Status403Forbidden);
        }

        private static object ToItem(NoteNumberingAdminService.Item i) => new
        {
            id = i.Id,
            kind = i.Kind.ToString(),
            kindLabel = NoteNumberingAdminService.KindLabel(i.Kind),
            prefix = i.Prefix,
            nextNumber = i.NextNumber,
            source = i.Source,
            branchName = i.BranchName,
            isDefault = i.IsDefault,
            inUse = i.InUse
        };

        private static object? ToCheck(NoteNumberingAdminService.DataicoCheck? c) => c == null ? null : new { @checked = c.Checked, exists = c.Exists, message = c.Message };

        private static object SavedResponse(NoteNumberingAdminService.SaveResult r) => new
        {
            id = r.Row!.Id,
            kind = r.Row.Kind.ToString(),
            prefix = r.Row.Prefix,
            nextNumber = r.Row.NextNumber ?? 1,
            isDefault = r.Row.IsDefault,
            dataico = ToCheck(r.Dataico)
        };

        [HttpGet("overview")]
        public async Task<IActionResult> Overview(Guid clientId)
        {
            var guard = await GuardAsync(clientId);
            if (guard != null) return guard;
            var overview = await _numberings.GetOverviewAsync(clientId);
            return Ok(new { usesDataico = overview.UsesDataico, items = overview.Items.Select(ToItem) });
        }

        // Prefijos que ya existen en la cuenta de Dataico del cliente y no están todavía como numeraciones propias.
        [HttpGet("dataico-prefixes")]
        public async Task<IActionResult> DataicoPrefixes(Guid clientId)
        {
            var guard = await GuardAsync(clientId);
            if (guard != null) return guard;
            var (missing, error) = await _numberings.DataicoPrefixesNotInFacilFacturaAsync(clientId);
            if (error != null) return BadRequest(new { message = error });
            return Ok(missing.Select(m => new { kind = m.Kind.ToString(), kindLabel = NoteNumberingAdminService.KindLabel(m.Kind), prefix = m.Prefix }));
        }

        [HttpPost("own")]
        public async Task<IActionResult> CreateOwn(Guid clientId, [FromBody] SaveOwnNumberingRequest request)
        {
            var guard = await GuardAsync(clientId);
            if (guard != null) return guard;
            if (!Enum.TryParse<NoteKind>(request.Kind, ignoreCase: true, out var kind) || !Enum.IsDefined(kind))
                return BadRequest(new { message = "El tipo de nota no es válido." });
            var result = await _numberings.CreateAsync(clientId, kind, request.Prefix, request.NextNumber);
            return result.Error != null ? BadRequest(new { message = result.Error }) : Ok(SavedResponse(result));
        }

        [HttpPut("own/{id:guid}")]
        public async Task<IActionResult> UpdateOwn(Guid clientId, Guid id, [FromBody] SaveOwnNumberingRequest request)
        {
            var guard = await GuardAsync(clientId);
            if (guard != null) return guard;
            var result = await _numberings.UpdateAsync(clientId, id, request.Prefix, request.NextNumber);
            return result.Error != null ? BadRequest(new { message = result.Error }) : Ok(SavedResponse(result));
        }

        [HttpDelete("own/{id:guid}")]
        public async Task<IActionResult> DeleteOwn(Guid clientId, Guid id)
        {
            var guard = await GuardAsync(clientId);
            if (guard != null) return guard;
            var error = await _numberings.DeleteAsync(clientId, id);
            return error != null ? BadRequest(new { message = error }) : NoContent();
        }

        [HttpPost("own/{id:guid}/default")]
        public async Task<IActionResult> SetDefault(Guid clientId, Guid id)
        {
            var guard = await GuardAsync(clientId);
            if (guard != null) return guard;
            var error = await _numberings.SetDefaultAsync(clientId, id);
            return error != null ? BadRequest(new { message = error }) : NoContent();
        }

        // Sin predeterminada propia, las notas de ese tipo usan el prefijo de su factura.
        [HttpPost("default/{kind}/clear")]
        public async Task<IActionResult> ClearDefault(Guid clientId, string kind)
        {
            var guard = await GuardAsync(clientId);
            if (guard != null) return guard;
            if (!Enum.TryParse<NoteKind>(kind, ignoreCase: true, out var noteKind) || !Enum.IsDefined(noteKind))
                return BadRequest(new { message = "El tipo de nota no es válido." });
            await _numberings.ClearDefaultAsync(clientId, noteKind);
            return NoContent();
        }

        [HttpGet("own/{id:guid}/dataico-check")]
        public async Task<IActionResult> DataicoCheck(Guid clientId, Guid id)
        {
            var guard = await GuardAsync(clientId);
            if (guard != null) return guard;
            var row = await _dbContext.NoteNumberings.AsNoTracking().FirstOrDefaultAsync(n => n.Id == id && n.ClientId == clientId && n.BranchId == null && n.ResolutionId == null);
            if (row == null || string.IsNullOrWhiteSpace(row.Prefix)) return NotFound();
            return Ok(ToCheck(await _numberings.CheckDataicoAsync(clientId, row.Kind, row.Prefix)));
        }
    }
}
