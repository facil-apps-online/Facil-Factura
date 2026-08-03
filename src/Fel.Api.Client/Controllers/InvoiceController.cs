using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Fel.Core.Entities;
using Fel.Infrastructure.Data;

namespace Fel.Api.Client.Controllers
{
    [ApiController]
    [Route("api/client/invoices")]
    public class InvoiceController : ControllerBase
    {
        private readonly FelDbContext _dbContext;

        public InvoiceController(FelDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        private Guid GetCurrentClientId()
        {
            if (Request.Headers.TryGetValue("x-client-id", out var clientIdStr))
            {
                if (Guid.TryParse(clientIdStr, out var clientId))
                    return clientId;
            }
            throw new UnauthorizedAccessException("x-client-id Header is missing");
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            try
            {
                var clientId = GetCurrentClientId();
                var invoices = await _dbContext.Documents
                    .Include(d => d.Customer)
                    .Where(d => d.ClientId == clientId)
                    .OrderByDescending(d => d.CreatedAt)
                    .ToListAsync();

                return Ok(invoices);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(ex.Message);
            }
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            try
            {
                var clientId = GetCurrentClientId();
                var invoice = await _dbContext.Documents
                    .Include(d => d.Customer)
                    .Include(d => d.Items)
                    .FirstOrDefaultAsync(d => d.Id == id && d.ClientId == clientId);

                if (invoice == null) return NotFound("Factura no encontrada.");
                return Ok(invoice);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(ex.Message);
            }
        }

        [HttpPost("draft")]
        public async Task<IActionResult> CreateDraft([FromBody] Document invoice)
        {
            try
            {
                var clientId = GetCurrentClientId();
                
                invoice.Id = Guid.NewGuid();
                invoice.ClientId = clientId;
                invoice.Status = "DRAFT";
                invoice.CreatedAt = DateTime.UtcNow;
                
                // Fetch the actual document type (for DianCode etc)
                if (invoice.DocumentTypeId.HasValue)
                {
                    var docType = await _dbContext.DocumentTypes.FindAsync(invoice.DocumentTypeId.Value);
                    if (docType != null) invoice.TypeCode = docType.Code;
                }

                foreach (var item in invoice.Items)
                {
                    item.Id = Guid.NewGuid();
                    item.DocumentId = invoice.Id;
                }

                _dbContext.Documents.Add(invoice);
                await _dbContext.SaveChangesAsync();

                return CreatedAtAction(nameof(GetById), new { id = invoice.Id }, invoice);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(ex.Message);
            }
        }
        
        [HttpPut("{id}/draft")]
        public async Task<IActionResult> UpdateDraft(Guid id, [FromBody] Document invoiceData)
        {
            try
            {
                var clientId = GetCurrentClientId();
                var invoice = await _dbContext.Documents
                    .Include(d => d.Items)
                    .FirstOrDefaultAsync(d => d.Id == id && d.ClientId == clientId);

                if (invoice == null) return NotFound("Factura no encontrada.");
                if (invoice.Status != "DRAFT") return BadRequest("Solo se pueden modificar borradores.");
                
                invoice.CustomerId = invoiceData.CustomerId;
                invoice.DocumentTypeId = invoiceData.DocumentTypeId;
                invoice.Notes = invoiceData.Notes;
                invoice.SectorExtensionData = invoiceData.SectorExtensionData;
                
                invoice.ReferenceDocumentId = invoiceData.ReferenceDocumentId;
                invoice.ReferenceConcept = invoiceData.ReferenceConcept;
                
                invoice.Subtotal = invoiceData.Subtotal;
                invoice.TaxAmount = invoiceData.TaxAmount;
                invoice.TotalAmount = invoiceData.TotalAmount;
                
                // Actualizar Items
                _dbContext.DocumentItems.RemoveRange(invoice.Items);
                
                foreach (var item in invoiceData.Items)
                {
                    item.Id = Guid.NewGuid();
                    item.DocumentId = invoice.Id;
                    invoice.Items.Add(item);
                }

                await _dbContext.SaveChangesAsync();
                return Ok(invoice);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(ex.Message);
            }
        }

        [HttpPost("{id}/publish")]
        public async Task<IActionResult> Publish(Guid id)
        {
            try
            {
                var clientId = GetCurrentClientId();
                var invoice = await _dbContext.Documents
                    .FirstOrDefaultAsync(d => d.Id == id && d.ClientId == clientId);

                if (invoice == null) return NotFound("Factura no encontrada.");
                
                if (invoice.Status != "DRAFT")
                    return BadRequest("La factura ya fue emitida o está en proceso.");

                // Cambiar estado a PROCESSING para que el motor de DIAN la tome
                invoice.Status = "PROCESSING";
                
                await _dbContext.SaveChangesAsync();

                // TODO: Enviar al Queue o disparar servicio DIAN directamente.

                return Ok(new { message = "Factura encolada para emisión a la DIAN", invoice });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(ex.Message);
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteDraft(Guid id)
        {
            try
            {
                var clientId = GetCurrentClientId();
                var invoice = await _dbContext.Documents
                    .FirstOrDefaultAsync(d => d.Id == id && d.ClientId == clientId);

                if (invoice == null) return NotFound("Factura no encontrada.");
                if (invoice.Status != "DRAFT") return BadRequest("Solo se pueden eliminar borradores.");

                _dbContext.Documents.Remove(invoice);
                await _dbContext.SaveChangesAsync();
                return NoContent();
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(ex.Message);
            }
        }
    }
}
