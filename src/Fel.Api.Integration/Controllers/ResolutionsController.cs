using System;
using System.Linq;
using System.Threading.Tasks;
using Fel.Api.Integration.Security;
using Fel.Infrastructure.Data;
using Fel.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Fel.Api.Integration.Controllers
{
    /// <summary>
    /// Resoluciones de numeración del emisor autenticado: sirven para saber qué prefijo y rango se pueden enviar en <c>number</c>.
    /// </summary>
    [ApiController]
    [Route("api/co/dian/resolutions")]
    public class ResolutionsController : ControllerBase
    {
        private readonly FelDbContext _dbContext;
        private readonly DataicoResolutionSyncService _dataicoSync;

        public ResolutionsController(FelDbContext dbContext, DataicoResolutionSyncService dataicoSync)
        {
            _dbContext = dbContext;
            _dataicoSync = dataicoSync;
        }

        /// <summary>
        /// Lista las resoluciones del emisor que se pueden usar en la sucursal de la API Key. <c>nextNumber</c> es una referencia: el
        /// consecutivo lo lleva el integrador.
        /// </summary>
        /// <param name="documentType">Opcional. Tipo de documento de la resolución, por ejemplo FE (factura de venta) o DS (documento soporte).</param>
        /// <response code="200">Lista de resoluciones activas.</response>
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> List([FromQuery] string? documentType = null)
        {
            if (!TryGetIssuer(out var clientId, out var branchId, out var unauthorized)) return unauthorized!;
            return Ok(await QueryAsync(clientId, branchId, documentType));
        }

        /// <summary>
        /// Trae las resoluciones vigentes que el integrador (Dataico) tiene vinculadas al NIT del emisor y las deja disponibles. Solo aplica a
        /// emisores que facturan por Dataico; con emisión directa a la DIAN las resoluciones se cargan desde el portal.
        /// </summary>
        /// <response code="200">Resultado de la sincronización y la lista actualizada.</response>
        /// <response code="409">El emisor no factura por Dataico, así que no hay nada que sincronizar.</response>
        [HttpPost("sync")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Sync()
        {
            if (!TryGetIssuer(out var clientId, out var branchId, out var unauthorized)) return unauthorized!;

            if (!await _dataicoSync.UsesDataicoAsync(clientId))
                return Conflict(new
                {
                    code = "SYNC_NOT_APPLICABLE",
                    message = "Este emisor factura directo a la DIAN: sus resoluciones se cargan desde el portal (Resoluciones), no hay nada que sincronizar."
                });

            var report = await _dataicoSync.SyncAsync(clientId);
            return Ok(new
            {
                created = report.Created,
                updated = report.Updated,
                reactivated = report.Reactivated,
                unchanged = report.Unchanged,
                notes = report.Notes,
                errors = report.Errors,
                resolutions = await QueryAsync(clientId, branchId, null)
            });
        }

        private async Task<object> QueryAsync(Guid clientId, Guid branchId, string? documentType)
        {
            var today = DateTime.Today;
            var query = _dbContext.Resolutions.AsNoTracking()
                .Where(r => r.ClientId == clientId && r.IsActive && _dbContext.ResolutionBranches.Any(rb => rb.ResolutionId == r.Id && rb.BranchId == branchId));
            if (!string.IsNullOrWhiteSpace(documentType)) query = query.Where(r => r.DocumentType == documentType.Trim().ToUpper());

            var rows = await query.OrderBy(r => r.DocumentType).ThenBy(r => r.Prefix).ThenBy(r => r.ResolutionNumber).ToListAsync();
            return rows.Select(r => new
            {
                documentType = r.DocumentType,
                prefix = r.Prefix,
                resolutionNumber = r.ResolutionNumber,
                numberStart = r.NumberStart,
                numberEnd = r.NumberEnd,
                nextNumber = r.NextNumber,
                validFrom = r.ValidFrom,
                validTo = r.ValidTo,
                expired = r.ValidTo.Date < today
            }).ToList();
        }

        private bool TryGetIssuer(out Guid clientId, out Guid branchId, out IActionResult? unauthorized)
        {
            clientId = default;
            branchId = default;
            unauthorized = null;
            if (HttpContext.Items["ClientId"] is not string text || !Guid.TryParse(text, out clientId) || HttpContext.GetBranchId() is not Guid branch)
            {
                unauthorized = Unauthorized(new { code = "INVALID_API_KEY", message = "No se pudo resolver el emisor autenticado." });
                return false;
            }
            branchId = branch;
            return true;
        }
    }
}
