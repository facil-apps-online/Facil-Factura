using System;
using System.Threading.Tasks;
using Fel.Core.Interfaces;
using Fel.Core.Models;
using Microsoft.AspNetCore.Mvc;

namespace Fel.Api.Integration.Controllers
{
    [ApiController]
    [Route("api/transport-invoices")]
    public class TransportInvoicesController : ControllerBase
    {
        private readonly IMessageQueue _messageQueue;
        private const string QueueName = "fel:documents:queue";

        public TransportInvoicesController(IMessageQueue messageQueue)
        {
            _messageQueue = messageQueue;
        }

        /// <summary>
        /// Endpoint exclusivo para el Sector Transporte (Carga RNDC / Remesas)
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> ReceiveTransportInvoice([FromBody] TransportInvoiceRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.DocumentNumber))
                return BadRequest("El número de factura de transporte es obligatorio.");

            if (request.IssueDate == default) request.IssueDate = DateTime.UtcNow;

            await _messageQueue.EnqueueAsync(QueueName, new { Type = "INVOICE_TRANSPORT", Payload = request });

            return Accepted(new 
            { 
                Message = "Factura Sector Transporte recibida y encolada para procesamiento (RNDC/DIAN).", 
                TrackingId = $"TRANS-{request.Prefix}{request.DocumentNumber}",
                Status = "PENDING"
            });
        }
    }
}
