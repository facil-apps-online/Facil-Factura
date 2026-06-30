using System;
using System.Threading.Tasks;
using Fel.Core.Interfaces;
using Fel.Core.Models;
using Microsoft.AspNetCore.Mvc;

namespace Fel.Api.Controllers
{
    [ApiController]
    [Route("api/habilitacion")]
    public class HabilitacionController : ControllerBase
    {
        private readonly IMessageQueue _messageQueue;
        private const string QueueName = "fel:invoices:queue";

        public HabilitacionController(IMessageQueue messageQueue)
        {
            _messageQueue = messageQueue;
        }

        [HttpPost("ejecutar-set-pruebas")]
        public async Task<IActionResult> EjecutarSetPruebas([FromBody] SetPruebasRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.TestSetId))
            {
                return BadRequest("El TestSetId (PIN de la DIAN) es obligatorio.");
            }

            // Para habilitar a un emisor en la DIAN, se requiere el envío de un SET de pruebas:
            // Usualmente la DIAN exige: 8 Facturas Electrónicas, 1 Nota Crédito, 1 Nota Débito.

            int startNumber = 9900000;
            
            // Simular el encolamiento de las 8 Facturas requeridas por la DIAN
            for (int i = 1; i <= 8; i++)
            {
                var invoice = new UblInvoiceData
                {
                    Prefix = "SETP",
                    DocumentNumber = (startNumber + i).ToString(),
                    IssueDate = DateTime.UtcNow,
                    IssueTime = DateTime.UtcNow,
                    LineExtensionAmount = 100000m,
                    TaxExclusiveAmount = 100000m,
                    TaxInclusiveAmount = 119000m,
                    PayableAmount = 119000m,
                    Currency = "COP",
                    Issuer = new IssuerData { Name = "Empresa Pruebas", TaxId = "900000000" },
                    Customer = new CustomerData { Name = "Adquirente Pruebas", TaxId = "800000000" },
                    Lines = new System.Collections.Generic.List<InvoiceLine> 
                    {
                        new InvoiceLine { Description = "Producto Set de Prueba", Quantity = 1, UnitPrice = 100000m, LineExtensionAmount = 100000m }
                    },
                    Taxes = new System.Collections.Generic.List<TaxSubtotal>
                    {
                        new TaxSubtotal { TaxId = "01", TaxableAmount = 100000m, TaxAmount = 19000m, Percent = 19m }
                    }
                    // En el modelo real se incluiría el TestSetId en la Extensión de la DIAN para que el Worker lo estampe
                };

                await _messageQueue.EnqueueAsync(QueueName, invoice);
            }

            // 2. Encolar Nota Crédito
            var nc = new UblInvoiceData
            {
                Prefix = "SETNC",
                DocumentNumber = "9900001",
                IssueDate = DateTime.UtcNow,
                IssueTime = DateTime.UtcNow,
                LineExtensionAmount = 50000m,
                TaxExclusiveAmount = 50000m,
                TaxInclusiveAmount = 59500m,
                PayableAmount = 59500m,
                Currency = "COP",
                Issuer = new IssuerData { Name = "Empresa Pruebas", TaxId = "900000000" },
                Customer = new CustomerData { Name = "Adquirente Pruebas", TaxId = "800000000" },
                Lines = new System.Collections.Generic.List<InvoiceLine> 
                {
                    new InvoiceLine { Description = "Anulación Parcial Set de Prueba", Quantity = 1, UnitPrice = 50000m, LineExtensionAmount = 50000m }
                },
                Taxes = new System.Collections.Generic.List<TaxSubtotal>
                {
                    new TaxSubtotal { TaxId = "01", TaxableAmount = 50000m, TaxAmount = 9500m, Percent = 19m }
                }
            };
            await _messageQueue.EnqueueAsync(QueueName, nc);

            // 3. Encolar Nota Débito
            var nd = new UblInvoiceData
            {
                Prefix = "SETND",
                DocumentNumber = "9900001",
                IssueDate = DateTime.UtcNow,
                IssueTime = DateTime.UtcNow,
                LineExtensionAmount = 20000m,
                TaxExclusiveAmount = 20000m,
                TaxInclusiveAmount = 23800m,
                PayableAmount = 23800m,
                Currency = "COP",
                Issuer = new IssuerData { Name = "Empresa Pruebas", TaxId = "900000000" },
                Customer = new CustomerData { Name = "Adquirente Pruebas", TaxId = "800000000" },
                Lines = new System.Collections.Generic.List<InvoiceLine> 
                {
                    new InvoiceLine { Description = "Intereses Set de Prueba", Quantity = 1, UnitPrice = 20000m, LineExtensionAmount = 20000m }
                },
                Taxes = new System.Collections.Generic.List<TaxSubtotal>
                {
                    new TaxSubtotal { TaxId = "01", TaxableAmount = 20000m, TaxAmount = 3800m, Percent = 19m }
                }
            };
            await _messageQueue.EnqueueAsync(QueueName, nd);

            return Ok(new
            {
                Message = "Script de habilitación iniciado. Se han encolado los documentos requeridos al Worker.",
                TestSetId = request.TestSetId,
                DocumentsEnqueued = 10
            });
        }
    }

    public class SetPruebasRequest
    {
        public string TestSetId { get; set; } = string.Empty;
        public string ClientId { get; set; } = string.Empty;
    }
}
