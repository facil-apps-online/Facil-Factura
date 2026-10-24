using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Fel.Core.Entities;
using Fel.Infrastructure.Data;
using Fel.Infrastructure.Dian;
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
        private readonly AcquirerLookupService _acquirerLookup;
        private readonly Fel.Infrastructure.Services.ClientPlanUsageService _planUsage;
        private readonly Fel.Api.Client.Security.ClientSessionIssuer _sessionIssuer;

        public ClientSessionController(FelDbContext dbContext, AcquirerLookupService acquirerLookup, Fel.Infrastructure.Services.ClientPlanUsageService planUsage,
            Fel.Api.Client.Security.ClientSessionIssuer sessionIssuer)
        {
            _dbContext = dbContext;
            _acquirerLookup = acquirerLookup;
            _planUsage = planUsage;
            _sessionIssuer = sessionIssuer;
        }

        // Empresas del mismo correo en este tenant, para el selector del portal. Vacía (no se muestra) cuando solo hay una, o en una sesión de soporte.
        private async Task<List<object>> CompaniesAsync()
        {
            var me = await _dbContext.ClientUsers.AsNoTracking()
                .Where(u => u.Id == Branch.UserId)
                .Select(u => new { u.Email, TenantId = u.Client.TenantId })
                .FirstOrDefaultAsync();
            if (me == null) return new List<object>();

            var companies = await _sessionIssuer.CompaniesOfAsync(me.TenantId, me.Email);
            if (companies.Count < 2) return new List<object>();
            return companies.Select(u => (object)new { clientId = u.ClientId, name = u.Client.CompanyName, taxId = u.Client.TaxId }).ToList();
        }

        public sealed class SwitchCompanyRequest
        {
            public System.Guid ClientId { get; set; }
        }

        // Cambia de empresa sin pedir la contraseña otra vez: la persona ya está autenticada, y solo se le entrega la sesión de OTRO usuario con su mismo
        // correo en el mismo tenant. Devuelve lo mismo que el inicio de sesión.
        [HttpPost("switch-company")]
        public async Task<IActionResult> SwitchCompany([FromBody] SwitchCompanyRequest request)
        {
            var me = await _dbContext.ClientUsers.AsNoTracking()
                .Where(u => u.Id == Branch.UserId)
                .Select(u => new { u.Email, TenantId = u.Client.TenantId })
                .FirstOrDefaultAsync();
            if (me == null) return BadRequest("Esta sesión no puede cambiar de empresa.");

            var target = (await _sessionIssuer.CompaniesOfAsync(me.TenantId, me.Email)).FirstOrDefault(u => u.ClientId == request.ClientId);
            if (target == null) return BadRequest("No tienes acceso a esa empresa.");

            var tenant = await _dbContext.Tenants.FirstAsync(t => t.Id == me.TenantId);
            var issued = await _sessionIssuer.IssueAsync(target, tenant);
            if (!issued.Ok) return StatusCode(issued.Status, issued.Error);
            return Ok(issued.Payload);
        }

        // Consumo de documentos del cliente frente a su plan, solo si el tenant decidió mostrárselo (Tenant.ShowUsageToClients). Sin valores en dinero:
        // lo que se cobra lo maneja el tenant. Cuelga de "session" porque es un prefijo permitido del portal.
        [HttpGet("plan-usage")]
        public async Task<IActionResult> PlanUsage()
        {
            var visible = await _dbContext.Clients.AsNoTracking().Where(c => c.Id == Branch.ClientId).Select(c => c.Tenant!.ShowUsageToClients).FirstOrDefaultAsync();
            if (!visible) return Ok(new { visible = false });

            var usage = await _planUsage.GetUsageAsync(Branch.ClientId, System.DateTime.UtcNow);
            if (usage == null) return Ok(new { visible = true, hasPlan = false });

            var allowed = Branch.AllowedBranchIds;
            var units = usage.Units
                .Where(u => u.BranchId == null || allowed.Contains(u.BranchId.Value))
                .Select(u => new { u.Name, u.Documents, u.Included, u.Extra })
                .ToList();
            return Ok(new { visible = true, hasPlan = true, planName = usage.PlanName, cycle = usage.Cycle, periodStart = usage.PeriodStart, periodEnd = usage.PeriodEnd, units });
        }

        [HttpGet]
        public async Task<IActionResult> Get()
        {
            var allowedIds = Branch.AllowedBranchIds;
            var branches = await _dbContext.Branches.AsNoTracking()
                .Where(b => allowedIds.Contains(b.Id))
                .OrderByDescending(b => b.IsMain).ThenBy(b => b.Name)
                .Select(b => new { b.Id, b.Name, b.Code, b.IsMain, b.CityCode })
                .ToListAsync();

            var client = await _dbContext.Clients.AsNoTracking().Where(c => c.Id == Branch.ClientId)
                .Select(c => new { c.ControlsPayments, c.ForeignCurrencyEnabled, c.RoundingAdjustment, c.Latitude, c.Longitude, IsDataico = c.Integrator.Code == "DATAICO" })
                .FirstOrDefaultAsync();

            return Ok(new
            {
                role = Branch.Role,
                isAdministrator = Branch.IsAdministrator,
                allBranches = Branch.CanViewAllBranches,
                branches,
                // Módulos que el Tenant activó para este cliente: el portal oculta el menú y la ruta de lo que esté apagado.
                features = new
                {
                    payments = client?.ControlsPayments ?? false, foreignCurrency = client?.ForeignCurrencyEnabled ?? false, roundingAdjustment = client?.RoundingAdjustment ?? "None", dianLookup = await _acquirerLookup.IsAvailableAsync(Branch.ClientId), dataico = client?.IsDataico ?? false,
                    // Esta empresa es la propietaria de su tenant (la que factura a los demás): puede sincronizar los clientes del tenant como terceros.
                    tenantOwner = await _dbContext.TenantInvoicingSettings.AsNoTracking().AnyAsync(s => s.OwnerClientId == Branch.ClientId)
                },
                // Ubicación de la empresa: el buscador de direcciones la usa para mostrar primero lo que queda cerca.
                location = client?.Latitude != null && client.Longitude != null ? new { latitude = client.Latitude, longitude = client.Longitude } : null,
                // Empresas a las que puede pasar quien tiene el mismo correo en varios clientes del tenant (vacía si solo tiene una).
                companies = await CompaniesAsync(),
                roles = ClientUserRoles.All.Select(r => new { value = r.Value, label = r.Label }),
                noteKinds = NoteKinds.All.Select(k => new { value = k.Kind.ToString(), label = k.Label })
            });
        }
    }
}
