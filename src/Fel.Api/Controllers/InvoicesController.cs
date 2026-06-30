using System;
using System.Threading.Tasks;
using Fel.Core.Interfaces;
using Fel.Core.Models;
using Microsoft.AspNetCore.Mvc;

namespace Fel.Api.Controllers
{
    [ApiController]
    [Route("api/invoices")]
    public class InvoicesController : ControllerBase
    {
        private readonly IMessageQueue _messageQueue;
        private const string QueueName = "fel:invoices:queue";

        public InvoicesController(IMessageQueue messageQueue)
        {
            _messageQueue = messageQueue;
        }

        [HttpPost]
        public async Task<IActionResult> ReceiveInvoice([FromBody] InvoiceRequest request)
        {
            // Validaciones básicas antes de aceptar el request
            if (string.IsNullOrWhiteSpace(request.DocumentNumber) || string.IsNullOrWhiteSpace(request.Prefix))
            {
                return BadRequest("El prefijo y el número de documento son obligatorios.");
            }

            if (request.IssueDate == default)
            {
                request.IssueDate = DateTime.UtcNow;
            }

            // Encolar en Redis
            await _messageQueue.EnqueueAsync(QueueName, request);

            // Devolver 202 Accepted con el Tracking ID o referencia
            return Accepted(new 
            { 
                Message = "Documento recibido y encolado para su procesamiento.", 
                TrackingId = $"{request.Prefix}{request.DocumentNumber}",
                Status = "PENDING"
            });
        }

    }
}
