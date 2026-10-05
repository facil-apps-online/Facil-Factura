using System;
using System.Threading.Tasks;
using Fel.Core.Interfaces;
using Fel.Core.Models;
using Fel.Infrastructure.Data;
using Fel.Infrastructure.Dian;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Fel.Api.Integration.Security;
using Fel.Infrastructure.Services;

namespace Fel.Api.Integration.Controllers
{
    /// <summary>
    /// Recepción de Notas Débito electrónicas (cargos adicionales sobre una factura ya emitida,
    /// o un ajuste no referenciado a ninguna factura puntual).
    /// </summary>
    [ApiController]
    [Route("api/co/dian/debit-notes")]
    public class DebitNotesController : ControllerBase
    {
        private readonly IMessageQueue _messageQueue;
        private readonly FelDbContext _dbContext;
        private readonly ICryptoVault _cryptoVault;
        private const string QueueName = "fel:invoices:queue";

        public DebitNotesController(IMessageQueue messageQueue, FelDbContext dbContext, ICryptoVault cryptoVault)
        {
            _messageQueue = messageQueue;
            _dbContext = dbContext;
            _cryptoVault = cryptoVault;
        }

        /// <summary>
        /// Recibe una Nota Débito y la encola para su procesamiento y envío a la DIAN. El emisor
        /// siempre es el Client autenticado por la API Key. Para referenciar una factura puntual,
        /// envía <c>BillingReferenceCufe</c> junto con <c>BillingReferenceDocumentNumber</c>; si los
        /// dejas vacíos, la nota se emite como no referenciada.
        /// </summary>
        /// <response code="202">La Nota Débito quedó encolada. Usa el <c>TrackingId</c> devuelto para consultar su estado en <c>GET /api/co/dian/documents/{trackId}/status</c>.</response>
        /// <response code="400">Falta el prefijo, el número de documento, o viene el CUFE de referencia sin el número de la factura original.</response>
        /// <response code="401">No se pudo resolver el emisor autenticado.</response>
        /// <response code="422">El emisor no está listo para facturar directo a la DIAN (integrador, resolución, certificado o NIT inconsistentes).</response>
        [HttpPost]
        [ProducesResponseType(StatusCodes.Status202Accepted)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
        public async Task<IActionResult> ReceiveDebitNote([FromBody] DebitNoteRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.DocumentNumber) || string.IsNullOrWhiteSpace(request.Prefix))
                return BadRequest("El prefijo y el número de documento son obligatorios para la Nota Débito.");

            if (!string.IsNullOrWhiteSpace(request.BillingReferenceCufe) && string.IsNullOrWhiteSpace(request.BillingReferenceDocumentNumber))
                return BadRequest("Si envías BillingReferenceCufe (nota referenciada), también debes enviar BillingReferenceDocumentNumber.");

            if (request.IssueDate == default) request.IssueDate = Fel.Core.Models.ColombiaTime.Now;

            if (HttpContext.Items["ClientId"] is not string clientIdStr || !Guid.TryParse(clientIdStr, out var clientId))
            {
                return Unauthorized(new { message = "No se pudo resolver el emisor autenticado." });
            }

            var client = await _dbContext.Clients.Include(c => c.Integrator).FirstOrDefaultAsync(c => c.Id == clientId);
            if (client == null) return Unauthorized(new { message = "Emisor no encontrado." });

            if (client.Integrator.Code != "NATIVE")
            {
                return UnprocessableEntity(new { message = $"Este emisor tiene configurado el proveedor '{client.Integrator.Code}', no emisión directa a la DIAN (NATIVE)." });
            }

            var resolution = await _dbContext.Resolutions.ForBranch(_dbContext, HttpContext.GetBranchId()).FirstOrDefaultAsync(
                r => r.ClientId == clientId && r.IsActive && r.DocumentType == "ND" && r.Prefix == request.Prefix);
            if (resolution == null)
            {
                return UnprocessableEntity(new { message = $"No hay una resolución de Nota Débito activa para el prefijo '{request.Prefix}'." });
            }

            var certificate = await _dbContext.Certificates.FirstOrDefaultAsync(c => c.ClientId == clientId && c.IsActive);
            if (certificate == null)
            {
                return UnprocessableEntity(new { message = "Este emisor no tiene un certificado digital activo cargado." });
            }

            var cert = _cryptoVault.GetCertificate(certificate.FileName, certificate.EncryptedPassword);
            var certificateNit = _cryptoVault.ExtractNit(cert);
            if (!NitValidation.Matches(certificateNit, client.TaxId))
            {
                return UnprocessableEntity(new { message = $"El NIT del certificado ({certificateNit ?? "no encontrado"}) no coincide con el NIT registrado del emisor ({client.TaxId})." });
            }

            var municipalities = await _dbContext.DianMunicipalities.AsNoTracking().ToDictionaryAsync(m => m.Code);
            var ublData = DianDocumentMapper.BuildDebitNoteDataFromRequest(request, client, resolution, municipalities);

            ublData.BranchId = HttpContext.GetBranchId();
            await _messageQueue.EnqueueAsync(QueueName, ublData);

            return Accepted(new
            {
                Message = "Nota Débito recibida y encolada para procesamiento.",
                TrackingId = $"{request.Prefix}{request.DocumentNumber}",
                Status = "PENDING"
            });
        }
    }
}
