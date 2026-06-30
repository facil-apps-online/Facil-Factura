using System;
using System.Threading.Tasks;
using Fel.Core.Interfaces;
using Fel.Core.Models;
using Microsoft.AspNetCore.Mvc;

namespace Fel.Api.Controllers
{
    [ApiController]
    [Route("api/equivalent-documents")]
    public class EquivalentDocumentsController : ControllerBase
    {
        private readonly IMessageQueue _messageQueue;
        private const string QueueName = "fel:documents:queue";

        public EquivalentDocumentsController(IMessageQueue messageQueue)
        {
            _messageQueue = messageQueue;
        }

        /// <summary>
        /// Emisión de Documento Equivalente Electrónico (Ej. Tiquete POS)
        /// Según Resolución 165 de la DIAN.
        /// </summary>
        [HttpPost("pos")]
        public async Task<IActionResult> ReceivePosDocument([FromBody] PosDocumentRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.DocumentNumber))
                return BadRequest("El número de documento POS es obligatorio.");

            if (request.IssueDate == default) request.IssueDate = DateTime.UtcNow;

            await _messageQueue.EnqueueAsync(QueueName, new { Type = "EQUIVALENT_DOCUMENT_POS", Payload = request });

            return Accepted(new 
            { 
                Message = "Documento Equivalente POS recibido y encolado para procesamiento.", 
                TrackingId = $"POS-{request.Prefix}{request.DocumentNumber}",
                Status = "PENDING"
            });
        }
    }
}
