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

namespace Fel.Api.Tenant.Controllers
{
    [ApiController]
    [Route("api/v1/{country}/{entity}/documents")]
    public class TenantDocumentsController : ControllerBase
    {
        private readonly FelDbContext _dbContext;
        private readonly IClinicalValidationService _clinicalValidationService;
        private readonly IMinSaludMuvService _muvService;

        public TenantDocumentsController(
            FelDbContext dbContext,
            IClinicalValidationService clinicalValidationService,
            IMinSaludMuvService muvService)
        {
            _dbContext = dbContext;
            _clinicalValidationService = clinicalValidationService;
            _muvService = muvService;
        }

        /// <summary>
        /// Emite una nueva Factura Electrónica de Venta.
        /// </summary>
        /// <response code="202">Factura aceptada y encolada.</response>
        /// <response code="400">Error de validación matemática o de estructura.</response>
        [HttpPost("invoices/emit")]
        [ProducesResponseType(StatusCodes.Status202Accepted)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
        public async Task<IActionResult> EmitInvoice(
            [FromRoute] string country,
            [FromRoute] string entity,
            [FromBody] InvoiceEmitRequest request)
        {
            if (!Request.Headers.ContainsKey("x-api-key")) return Unauthorized(new { message = "Falta x-api-key" });
            
            if (request.Subtotal + request.TaxAmount != request.TotalAmount)
                return BadRequest("Inconsistencia en totales matemáticos.");

            var trackingId = Guid.NewGuid().ToString("N");
            return Accepted(new { trackingId, status = "PROCESSING", message = $"Factura recibida para {entity.ToUpper()}" });
        }

        /// <summary>
        /// Emite una Nota Crédito Electrónica.
        /// </summary>
        /// <remarks>Debe incluir el ReferenceCufe de la factura a la cual afecta.</remarks>
        [HttpPost("credit-notes/emit")]
        [ProducesResponseType(StatusCodes.Status202Accepted)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> EmitCreditNote(
            [FromRoute] string country,
            [FromRoute] string entity,
            [FromBody] CreditNoteEmitRequest request)
        {
            if (!Request.Headers.ContainsKey("x-api-key")) return Unauthorized(new { message = "Falta x-api-key" });

            if (request.Subtotal + request.TaxAmount != request.TotalAmount)
                return BadRequest("Inconsistencia en totales matemáticos.");

            var trackingId = Guid.NewGuid().ToString("N");
            return Accepted(new { trackingId, status = "PROCESSING", message = $"Nota crédito recibida para {entity.ToUpper()}" });
        }

        /// <summary>
        /// Emite un Documento de Nómina Electrónica.
        /// </summary>
        [HttpPost("payroll/emit")]
        [ProducesResponseType(StatusCodes.Status202Accepted)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> EmitPayroll(
            [FromRoute] string country,
            [FromRoute] string entity,
            [FromBody] PayrollEmitRequest request)
        {
            if (!Request.Headers.ContainsKey("x-api-key")) return Unauthorized(new { message = "Falta x-api-key" });

            var trackingId = Guid.NewGuid().ToString("N");
            return Accepted(new { trackingId, status = "PROCESSING", message = $"Nómina recibida para {entity.ToUpper()}" });
        }

        /// <summary>
        /// Emite un archivo de RIPS (Salud) de forma independiente (Ej: Médicos particulares).
        /// Ideal para enviar a MinSalud sin estar atado a una factura electrónica.
        /// </summary>
        [HttpPost("rips/emit")]
        [ProducesResponseType(StatusCodes.Status202Accepted)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> EmitStandaloneRips(
            [FromRoute] string country,
            [FromRoute] string entity,
            [FromBody] RipsEmitRequest request)
        {
            if (!Request.Headers.ContainsKey("x-api-key")) return Unauthorized(new { message = "Falta x-api-key" });

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

            // Ensamblaje JSON y Envío al MUV
            var (isSuccess, cuv, message, jsonPayload) = await _muvService.SendRipsAsync(request);

            if (!isSuccess)
            {
                return StatusCode(StatusCodes.Status502BadGateway, new { message, error = "Error en integración MUV" });
            }

            return Accepted(new 
            { 
                trackingId = cuv, 
                status = "PROCESSING_MUV", 
                message = $"RIPS validado y aceptado correctamente para {entity.ToUpper()}",
                jsonPreview = jsonPayload // Solo para debug/desarrollo
            });
        }
    }
}
