using System;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Fel.Api.Tenant.DTOs;
using Fel.Core.Entities;
using Fel.Infrastructure.Data;
using Fel.Core.Interfaces;
using Fel.Api.Tenant.Services;
using Fel.Api.Tenant.Services.MinSalud;
using System.Linq;
using Microsoft.EntityFrameworkCore;

namespace Fel.Api.Tenant.Controllers
{
    /// <summary>
    /// RIPS (Registro Individual de Prestación de Servicios de Salud) enviado de forma
    /// independiente, sin factura electrónica de por medio — directo al MUV-FEV-RIPS de
    /// MinSalud. La ruta lleva el prefijo país/autoridad (<c>co/minsalud</c>) para poder sumar
    /// países nuevos más adelante sin romper esta. El RIPS que sí viaja adjunto a una factura se
    /// emite por la DIAN (ver <c>api/co/dian/health-invoices/rips</c> en Fel.Api.Integration).
    /// </summary>
    [ApiController]
    [Route("api/co/minsalud")]
    public class TenantDocumentsController : ControllerBase
    {
        private readonly FelDbContext _dbContext;
        private readonly IClinicalValidationService _clinicalValidationService;
        private readonly IMinSaludMuvService _muvService;
        private readonly Fel.Infrastructure.Services.BranchCredentialResolver _credentialResolver;

        public TenantDocumentsController(
            FelDbContext dbContext,
            IClinicalValidationService clinicalValidationService,
            IMinSaludMuvService muvService,
            Fel.Infrastructure.Services.BranchCredentialResolver credentialResolver)
        {
            _dbContext = dbContext;
            _clinicalValidationService = clinicalValidationService;
            _muvService = muvService;
            _credentialResolver = credentialResolver;
        }

        /// <summary>
        /// Emite un archivo de RIPS (Salud) de forma independiente (Ej: Médicos particulares).
        /// Ideal para enviar a MinSalud sin estar atado a una factura electrónica.
        /// </summary>
        [HttpPost("rips/emit")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> EmitStandaloneRips([FromBody] RipsEmitRequest request)
        {
            // El middleware HMAC ya validó la firma y dejó el Client resuelto en HttpContext.Items;
            // antes esta acción solo comprobaba que el header x-api-key existiera, sin usarlo, así
            // que nunca llegaba a las credenciales reales del prestador ante el MUV-FEV-RIPS.
            if (HttpContext.Items["ClientId"] is not string clientIdStr || !Guid.TryParse(clientIdStr, out var clientId))
            {
                return Unauthorized(new { message = "No se pudo resolver el emisor autenticado." });
            }
            var client = await _dbContext.Clients.FirstOrDefaultAsync(c => c.Id == clientId);
            if (client == null) return Unauthorized(new { message = "Emisor no encontrado." });

            // Validación básica
            if (string.IsNullOrWhiteSpace(request.ProviderCode))
                return BadRequest("El código de prestador (REPS) es obligatorio.");

            // Validación clínica estricta (MUV MinSalud)
            var clinicalErrors = await _clinicalValidationService.ValidateRipsAsync(request);
            if (clinicalErrors.Any())
            {
                return UnprocessableEntity(new
                {
                    message = "El RIPS ha sido rechazado por el Motor de Validación Clínica (MUV).",
                    errors = clinicalErrors
                });
            }

            // Ensamblaje JSON y Envío al MUV. A diferencia de la DIAN, CargarRipsSinFactura es
            // síncrono: si isSuccess viene en true es porque el MUV ya validó y entregó el CUV en
            // esta misma llamada — no queda nada "procesando" ni hay que consultar un estado luego.
            // Credenciales del prestador: las de la sucursal de la llave HMAC si tiene propias; si no, las del Client.
            Guid? branchId = HttpContext.Items["BranchId"] is string branchIdStr && Guid.TryParse(branchIdStr, out var parsedBranchId) ? parsedBranchId : null;
            var credentials = await _credentialResolver.MinSaludAsync(client, branchId);
            var (isSuccess, cuv, message, _) = await _muvService.SendRipsAsync(request, client, credentials);

            if (!isSuccess)
            {
                return StatusCode(StatusCodes.Status502BadGateway, new { message, error = "Error en integración MUV" });
            }

            return Ok(new
            {
                cuv,
                status = "ACCEPTED",
                message = $"RIPS validado y aceptado por MinSalud (MUV) para {client.CompanyName}."
            });
        }
    }
}
