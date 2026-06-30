using System;
using System.Threading.Tasks;
using Fel.Core.Interfaces;
using Fel.Core.Models;
using Microsoft.AspNetCore.Mvc;

namespace Fel.Api.Controllers
{
    [ApiController]
    [Route("api/reception-events")]
    public class ReceptionEventsController : ControllerBase
    {
        private readonly IMessageQueue _messageQueue;
        private const string QueueName = "fel:documents:queue";

        public ReceptionEventsController(IMessageQueue messageQueue)
        {
            _messageQueue = messageQueue;
        }

        /// <summary>
        /// Generación de eventos (Acuse de Recibo, Recibo del Bien, Aceptación, Reclamo)
        /// Necesarios para Título Valor (Radian) o deducción de costos.
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> ReceiveEvent([FromBody] ReceptionEventRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.DocumentNumber))
                return BadRequest("El UUID o CUFE de la factura afectada es obligatorio para el evento.");

            if (request.IssueDate == default) request.IssueDate = DateTime.UtcNow;

            await _messageQueue.EnqueueAsync(QueueName, new { Type = "RECEPTION_EVENT", Payload = request });

            return Accepted(new 
            { 
                Message = "Evento de Recepción (Acuse/Radian) recibido y encolado.", 
                TrackingId = $"EVT-{request.DocumentNumber.Substring(0, 8)}",
                Status = "PENDING"
            });
        }
    }
}
