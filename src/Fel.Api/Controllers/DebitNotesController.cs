using System;
using System.Threading.Tasks;
using Fel.Core.Interfaces;
using Fel.Core.Models;
using Microsoft.AspNetCore.Mvc;

namespace Fel.Api.Controllers
{
    [ApiController]
    [Route("api/debit-notes")]
    public class DebitNotesController : ControllerBase
    {
        private readonly IMessageQueue _messageQueue;
        private const string QueueName = "fel:documents:queue";

        public DebitNotesController(IMessageQueue messageQueue)
        {
            _messageQueue = messageQueue;
        }

        [HttpPost]
        public async Task<IActionResult> ReceiveDebitNote([FromBody] DebitNoteRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.DocumentNumber))
                return BadRequest("El número de documento es obligatorio para la Nota Débito.");

            if (request.IssueDate == default) request.IssueDate = DateTime.UtcNow;

            await _messageQueue.EnqueueAsync(QueueName, new { Type = "DEBIT_NOTE", Payload = request });

            return Accepted(new 
            { 
                Message = "Nota Débito recibida y encolada para procesamiento.", 
                TrackingId = $"ND-{request.Prefix}{request.DocumentNumber}",
                Status = "PENDING"
            });
        }
    }
}
