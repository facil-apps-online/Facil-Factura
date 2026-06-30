using System;
using System.Threading.Tasks;
using Fel.Core.Interfaces;
using Fel.Core.Models;
using Microsoft.AspNetCore.Mvc;

namespace Fel.Api.Controllers
{
    [ApiController]
    [Route("api/support-documents")]
    public class SupportDocumentsController : ControllerBase
    {
        private readonly IMessageQueue _messageQueue;
        private const string QueueName = "fel:documents:queue";

        public SupportDocumentsController(IMessageQueue messageQueue)
        {
            _messageQueue = messageQueue;
        }

        [HttpPost]
        public async Task<IActionResult> ReceiveSupportDocument([FromBody] SupportDocumentRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.DocumentNumber))
                return BadRequest("El número de documento soporte es obligatorio.");

            if (request.IssueDate == default) request.IssueDate = DateTime.UtcNow;

            await _messageQueue.EnqueueAsync(QueueName, new { Type = "SUPPORT_DOCUMENT", Payload = request });

            return Accepted(new 
            { 
                Message = "Documento Soporte recibido y encolado para procesamiento.", 
                TrackingId = $"DS-{request.Prefix}{request.DocumentNumber}",
                Status = "PENDING"
            });
        }
    }
}
