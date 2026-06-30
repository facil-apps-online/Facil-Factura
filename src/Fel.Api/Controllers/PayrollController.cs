using System;
using System.Threading.Tasks;
using Fel.Core.Interfaces;
using Fel.Core.Models;
using Microsoft.AspNetCore.Mvc;

namespace Fel.Api.Controllers
{
    [ApiController]
    [Route("api/payroll")]
    public class PayrollController : ControllerBase
    {
        private readonly IMessageQueue _messageQueue;
        private const string QueueName = "fel:documents:queue";

        public PayrollController(IMessageQueue messageQueue)
        {
            _messageQueue = messageQueue;
        }

        [HttpPost]
        public async Task<IActionResult> ReceivePayroll([FromBody] PayrollRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.DocumentNumber))
                return BadRequest("El número de documento de nómina es obligatorio.");

            if (request.IssueDate == default) request.IssueDate = DateTime.UtcNow;

            await _messageQueue.EnqueueAsync(QueueName, new { Type = "PAYROLL", Payload = request });

            return Accepted(new 
            { 
                Message = "Nómina electrónica recibida y encolada para procesamiento.", 
                TrackingId = $"NOM-{request.Prefix}{request.DocumentNumber}",
                Status = "PENDING"
            });
        }
    }
}
