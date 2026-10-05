using System;
using System.Linq;
using System.Threading.Tasks;
using Fel.Api.Security;
using Fel.Core.Entities;
using Fel.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Fel.Api.Client.Controllers
{
    // Numeración propia de notas de una sucursal. Por defecto las notas usan los contadores compartidos del Client (note-counters);
    // aquí se le da a una sucursal el suyo, con su propio prefijo, y se le puede quitar para que vuelva al compartido.
    [ApiController]
    [Route("api/client/note-numberings")]
    [ClientRole(ClientUserRoles.Administrator)]
    [AllowAllBranches]
    public class ClientNoteNumberingsController : ClientPortalControllerBase
    {
        private readonly FelDbContext _dbContext;

        public ClientNoteNumberingsController(FelDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public class SaveNoteNumberingRequest
        {
            public string Prefix { get; set; } = string.Empty;
            public long? NextNumber { get; set; }
        }

        [HttpGet]
        public async Task<IActionResult> List()
        {
            var clientId = GetCurrentClientId();
            var rows = await _dbContext.NoteNumberings.AsNoTracking()
                .Where(n => n.ClientId == clientId && n.BranchId != null)
                .Select(n => new { n.BranchId, BranchName = n.Branch!.Name, n.Kind, n.Prefix, n.NextNumber })
                .ToListAsync();

            return Ok(rows
                .OrderBy(r => r.BranchName).ThenBy(r => r.Kind)
                .Select(r => new
                {
                    branchId = r.BranchId,
                    branchName = r.BranchName,
                    kind = r.Kind.ToString(),
                    kindLabel = NoteKinds.All.First(k => k.Kind == r.Kind).Label,
                    prefix = r.Prefix,
                    nextNumber = r.NextNumber ?? 1
                }));
        }

        [HttpPut("{branchId:guid}/{kind}")]
        public async Task<IActionResult> Save(Guid branchId, string kind, [FromBody] SaveNoteNumberingRequest request)
        {
            var clientId = GetCurrentClientId();
            if (!Enum.TryParse<NoteKind>(kind, ignoreCase: true, out var noteKind) || !Enum.IsDefined(noteKind))
                return BadRequest("El tipo de nota no es válido.");
            if (!await _dbContext.Branches.AnyAsync(b => b.Id == branchId && b.ClientId == clientId && b.IsActive))
                return BadRequest("La sucursal no existe o está inactiva.");

            var prefix = (request.Prefix ?? string.Empty).Trim().ToUpperInvariant();
            if (prefix.Length == 0) return BadRequest("El prefijo es obligatorio en la numeración propia de una sucursal.");
            if (prefix.Length > 10) return BadRequest("El prefijo no puede tener más de 10 caracteres.");
            if (!prefix.All(char.IsLetterOrDigit)) return BadRequest("El prefijo solo puede tener letras y números.");
            if (request.NextNumber is < 1) return BadRequest("El consecutivo debe ser mayor a 0.");

            var row = await _dbContext.NoteNumberings.FirstOrDefaultAsync(n => n.ClientId == clientId && n.BranchId == branchId && n.Kind == noteKind);

            // El prefijo es único por cliente y tipo (también frente al contador compartido): la numeración se identifica por él.
            var currentId = row?.Id;
            var prefixTaken = await _dbContext.NoteNumberings.AnyAsync(n =>
                n.ClientId == clientId && n.Kind == noteKind && n.Prefix == prefix && (currentId == null || n.Id != currentId));
            if (prefixTaken) return BadRequest("Ese prefijo ya lo usa otra numeración de este tipo de nota.");

            if (row == null)
            {
                row = new NoteNumbering { Id = Guid.NewGuid(), ClientId = clientId, BranchId = branchId, Kind = noteKind };
                _dbContext.NoteNumberings.Add(row);
            }
            row.Prefix = prefix;
            if (request.NextNumber.HasValue) row.NextNumber = request.NextNumber.Value;

            await _dbContext.SaveChangesAsync();
            return Ok(new { branchId, kind = noteKind.ToString(), prefix = row.Prefix, nextNumber = row.NextNumber ?? 1 });
        }

        // Quitar la numeración propia: la sucursal vuelve a usar la compartida del Client.
        [HttpDelete("{branchId:guid}/{kind}")]
        public async Task<IActionResult> Remove(Guid branchId, string kind)
        {
            var clientId = GetCurrentClientId();
            if (!Enum.TryParse<NoteKind>(kind, ignoreCase: true, out var noteKind) || !Enum.IsDefined(noteKind))
                return BadRequest("El tipo de nota no es válido.");

            var row = await _dbContext.NoteNumberings.FirstOrDefaultAsync(n => n.ClientId == clientId && n.BranchId == branchId && n.Kind == noteKind);
            if (row == null) return NotFound();

            _dbContext.NoteNumberings.Remove(row);
            await _dbContext.SaveChangesAsync();
            return NoContent();
        }
    }
}
