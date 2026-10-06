using System.Linq;
using System.Threading.Tasks;
using Fel.Core.Entities;
using Fel.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Fel.Api.Client.Controllers
{
    // Lo que el portal necesita saber de la sesión al iniciar: rol, sucursales a las que puede entrar y los catálogos de roles y
    // tipos de nota. Se lee de la base en cada carga: la lista que devolvió el inicio de sesión queda vieja si se agrega una sucursal.
    [ApiController]
    [Route("api/client/session")]
    public class ClientSessionController : ClientPortalControllerBase
    {
        private readonly FelDbContext _dbContext;

        public ClientSessionController(FelDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        [HttpGet]
        public async Task<IActionResult> Get()
        {
            var allowedIds = Branch.AllowedBranchIds;
            var branches = await _dbContext.Branches.AsNoTracking()
                .Where(b => allowedIds.Contains(b.Id))
                .OrderByDescending(b => b.IsMain).ThenBy(b => b.Name)
                .Select(b => new { b.Id, b.Name, b.Code, b.IsMain })
                .ToListAsync();

            var controlsPayments = await _dbContext.Clients.AsNoTracking().Where(c => c.Id == Branch.ClientId).Select(c => c.ControlsPayments).FirstOrDefaultAsync();

            return Ok(new
            {
                role = Branch.Role,
                isAdministrator = Branch.IsAdministrator,
                allBranches = Branch.CanViewAllBranches,
                branches,
                // Módulos que el Tenant activó para este cliente: el portal oculta el menú y la ruta de lo que esté apagado.
                features = new { payments = controlsPayments },
                roles = ClientUserRoles.All.Select(r => new { value = r.Value, label = r.Label }),
                noteKinds = NoteKinds.All.Select(k => new { value = k.Kind.ToString(), label = k.Label })
            });
        }
    }
}
