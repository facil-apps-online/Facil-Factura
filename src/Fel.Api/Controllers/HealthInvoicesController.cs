using System;
using System.Threading.Tasks;
using Fel.Core.Interfaces;
using Fel.Core.Models;
using Microsoft.AspNetCore.Mvc;

namespace Fel.Api.Controllers
{
    [ApiController]
    [Route("api/health-invoices")]
    public class HealthInvoicesController : ControllerBase
    {
        private readonly IMessageQueue _messageQueue;
        private const string QueueName = "fel:documents:queue";

        public HealthInvoicesController(IMessageQueue messageQueue)
        {
            _messageQueue = messageQueue;
        }

        /// <summary>
        /// Endpoint exclusivo para Sector Salud (RIPS - MinSalud + DIAN)
        /// </summary>
        [HttpPost("rips")]
        public async Task<IActionResult> ReceiveRipsInvoice([FromBody] HealthInvoiceRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.DocumentNumber))
                return BadRequest("El número de factura sector salud es obligatorio.");

            if (request.IssueDate == default) request.IssueDate = DateTime.UtcNow;

            await _messageQueue.EnqueueAsync(QueueName, new { Type = "INVOICE_HEALTH_RIPS", Payload = request });

            return Accepted(new 
            { 
                Message = "Factura Sector Salud con RIPS recibida y encolada para procesamiento en SISPRO y DIAN.", 
                TrackingId = $"SALUD-{request.Prefix}{request.DocumentNumber}",
                Status = "PENDING"
            });
        }
    }
}
