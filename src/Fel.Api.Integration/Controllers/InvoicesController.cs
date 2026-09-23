using System;
using System.Threading.Tasks;
using Fel.Core.Interfaces;
using Fel.Core.Models;
using Fel.Infrastructure.Data;
using Fel.Infrastructure.Dian;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Fel.Api.Integration.Controllers
{
    /// <summary>
    /// Recepción de Facturas Electrónicas de Venta estándar (la mayoría de los casos de uso).
    /// Para sectores con requisitos propios usa el endpoint específico: salud
    /// (<c>/api/co/dian/health-invoices/rips</c>) o transporte (<c>/api/co/dian/transport-invoices</c>).
    /// </summary>
    [ApiController]
    [Route("api/co/dian/invoices")]
    public class InvoicesController : ControllerBase
    {
        private readonly IMessageQueue _messageQueue;
        private readonly FelDbContext _dbContext;
        private readonly ICryptoVault _cryptoVault;
        private const string QueueName = "fel:invoices:queue";

        public InvoicesController(IMessageQueue messageQueue, FelDbContext dbContext, ICryptoVault cryptoVault)
        {
            _messageQueue = messageQueue;
            _dbContext = dbContext;
            _cryptoVault = cryptoVault;
        }

        /// <summary>
        /// Recibe una Factura Electrónica de Venta estándar y la encola para su procesamiento y
        /// emisión directa ante la DIAN con el certificado propio del emisor autenticado.
        /// </summary>
        /// <param name="request">Datos de la factura: adquirente, ítems, impuestos y totales. El emisor
        /// siempre es el Client autenticado por la API Key — este API no acepta facturar a nombre de otro.</param>
        /// <response code="202">La factura quedó encolada. Usa el <c>TrackingId</c> devuelto para consultar su estado en <c>GET /api/co/dian/documents/{trackId}/status</c>.</response>
        /// <response code="400">Falta el prefijo o el número de documento.</response>
        /// <response code="401">No se pudo resolver el emisor autenticado.</response>
        /// <response code="422">El emisor no está listo para facturar directo a la DIAN (integrador, resolución, certificado o NIT inconsistentes).</response>
        [HttpPost]
        [ProducesResponseType(StatusCodes.Status202Accepted)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
        public async Task<IActionResult> ReceiveInvoice([FromBody] InvoiceRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.DocumentNumber) || string.IsNullOrWhiteSpace(request.Prefix))
            {
                return BadRequest("El prefijo y el número de documento son obligatorios.");
            }

            if (request.IssueDate == default)
            {
                request.IssueDate = DateTime.UtcNow;
            }

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

            var resolution = await _dbContext.Resolutions.FirstOrDefaultAsync(
                r => r.ClientId == clientId && r.IsActive && r.DocumentType == "FE" && r.Prefix == request.Prefix);
            if (resolution == null)
            {
                return UnprocessableEntity(new { message = $"No hay una resolución de facturación activa para el prefijo '{request.Prefix}'." });
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
            var ublData = DianDocumentMapper.BuildInvoiceDataFromRequest(request, client, resolution, municipalities);

            await _messageQueue.EnqueueAsync(QueueName, ublData);

            return Accepted(new
            {
                Message = "Documento recibido y encolado para su procesamiento.",
                TrackingId = $"{request.Prefix}{request.DocumentNumber}",
                Status = "PENDING"
            });
        }
    }
}
