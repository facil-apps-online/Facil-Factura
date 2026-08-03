using System;
using System.Threading.Tasks;
using Fel.Core.Interfaces;
using Fel.Core.Models;
using Microsoft.AspNetCore.Mvc;

namespace Fel.Api.Integration.Controllers
{
    [ApiController]
    [Route("api/credit-notes")]
    public class CreditNotesController : ControllerBase
    {
        private readonly IMessageQueue _messageQueue;
        private const string QueueName = "fel:documents:queue";

        public CreditNotesController(IMessageQueue messageQueue)
        {
            _messageQueue = messageQueue;
        }

        [HttpPost]
        public async Task<IActionResult> ReceiveCreditNote([FromBody] CreditNoteRequest request) 
        {
            // Validaciones específicas de Nota Crédito
            if (string.IsNullOrWhiteSpace(request.DocumentNumber) || string.IsNullOrWhiteSpace(request.Prefix))
                return BadRequest("El prefijo y el número de documento son obligatorios para la Nota Crédito.");

            if (request.IssueDate == default) request.IssueDate = DateTime.UtcNow;

            await _messageQueue.EnqueueAsync(QueueName, new { Type = "CREDIT_NOTE", Payload = request });

            return Accepted(new 
            { 
                Message = "Nota Crédito recibida y encolada para procesamiento.", 
                TrackingId = $"NC-{request.Prefix}{request.DocumentNumber}",
                Status = "PENDING"
            });
        }
    }
}
