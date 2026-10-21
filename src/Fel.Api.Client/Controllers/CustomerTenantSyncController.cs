using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Fel.Api.Security;
using Fel.Core.Entities;
using Fel.Infrastructure.Data;
using Fel.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Fel.Api.Client.Controllers
{
    // Sincroniza los clientes del tenant como terceros tipo Cliente de la empresa propietaria del tenant. Solo la empresa propietaria (la que el tenant
    // eligió como emisora) puede usarlo, y solo ve a los clientes de su propio tenant. Cuelga de "customers", un prefijo permitido del portal.
    [ApiController]
    [Route("api/client/customers/tenant-sync")]
    [AllowAllBranches]
    [ClientRole(ClientUserRoles.Administrator)]
    public class CustomerTenantSyncController : ClientPortalControllerBase
    {
        private readonly FelDbContext _dbContext;
        private readonly OwnerCustomerSyncService _sync;

        public CustomerTenantSyncController(FelDbContext dbContext, OwnerCustomerSyncService sync)
        {
            _dbContext = dbContext;
            _sync = sync;
        }

        private Task<bool> IsOwnerAsync() =>
            _dbContext.TenantInvoicingSettings.AsNoTracking().AnyAsync(s => s.OwnerClientId == GetCurrentClientId());

        [HttpGet("preview")]
        public async Task<IActionResult> Preview()
        {
            if (!await IsOwnerAsync()) return Forbid();
            var items = await _sync.PreviewAsync(GetCurrentClientId());
            return Ok(new
            {
                items,
                summary = new
                {
                    toCreate = items.Count(i => i.State == "ToCreate"), toComplete = items.Count(i => i.State == "ToComplete"),
                    upToDate = items.Count(i => i.State == "UpToDate"), review = items.Count(i => i.State == "Review"),
                    incomplete = items.Count(i => i.Missing.Count > 0)
                }
            });
        }

        public class ApplyRequest
        {
            // Clientes del tenant a sincronizar; vacío = todos los que se pueden crear o completar.
            public List<Guid>? ClientIds { get; set; }
        }

        [HttpPost("apply")]
        public async Task<IActionResult> Apply([FromBody] ApplyRequest request)
        {
            if (!await IsOwnerAsync()) return Forbid();
            var result = await _sync.ApplyAsync(GetCurrentClientId(), request.ClientIds);
            return Ok(new { result.Created, result.Completed });
        }
    }
}
