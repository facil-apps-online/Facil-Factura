using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using Fel.Core.Entities;
using Fel.Core.Interfaces;
using Fel.Infrastructure.Data;
using Fel.Infrastructure.Dian;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Fel.Api.Security;
using Fel.Infrastructure.Services;

namespace Fel.Api.Client.Controllers
{
    /// <summary>
    /// Documentos electrónicos de terceros (facturas/notas de proveedores) recibidos por este
    /// Client, y los Eventos de Recepción RADIAN que se disparan sobre ellos.
    /// </summary>
    [ApiController]
    [Route("api/client/received-documents")]
    [ClientRole(ClientUserRoles.Administrator)]
    public class ReceivedDocumentsController : ClientPortalControllerBase
    {
        private readonly FelDbContext _dbContext;
        private readonly IReceptionEventService _eventService;

        public ReceivedDocumentsController(FelDbContext dbContext, IReceptionEventService eventService)
        {
            _dbContext = dbContext;
            _eventService = eventService;
        }

        [HttpGet]
        public async Task<IActionResult> GetReceivedDocuments()
        {
            try
            {
                var clientId = GetCurrentClientId();
                var documents = await _dbContext.ReceivedDocuments.ForBranch(CurrentBranchScope)
                    .Where(d => d.ClientId == clientId)
                    .Include(d => d.Events)
                    .OrderByDescending(d => d.ReceivedAt)
                    .ToListAsync();

                return Ok(documents.Select(d => new
                {
                    d.Id,
                    d.SourceType,
                    d.Cufe,
                    d.DocumentTypeCode,
                    d.IssuerTaxId,
                    d.IssuerName,
                    d.DocumentId,
                    d.IssueDate,
                    d.TotalAmount,
                    d.ReceivedAt,
                    Events = d.Events.Select(e => new { e.EventCode, e.Status, e.SentAt, e.DianResponseMessage })
                }));
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(ex.Message);
            }
        }

        /// <summary>
        /// Sube manualmente el ZIP o el XML de una factura/nota de un proveedor. Si el Client tiene
        /// eventos configurados para autoenviarse, se disparan de una vez.
        /// </summary>
        [HttpPost("upload")]
        public async Task<IActionResult> Upload(IFormFile file)
        {
            Guid clientId;
            try
            {
                clientId = GetCurrentClientId();
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(ex.Message);
            }

            if (file == null || file.Length == 0)
                return BadRequest("No se proporcionó un archivo válido.");

            string xml;
            try
            {
                xml = await ExtractXmlAsync(file);
            }
            catch (Exception ex)
            {
                return BadRequest($"No se pudo leer el archivo: {ex.Message}");
            }

            ParsedReceivedDocument parsed;
            try
            {
                parsed = ReceivedDocumentParser.Parse(xml);
            }
            catch (Exception ex)
            {
                return BadRequest($"No se pudo interpretar el XML: {ex.Message}");
            }

            var client = await _dbContext.Clients.FirstOrDefaultAsync(c => c.Id == clientId);
            if (client == null) return Unauthorized(new { message = "Emisor no encontrado." });

            var alreadyExists = await _dbContext.ReceivedDocuments.AnyAsync(d => d.ClientId == clientId && d.Cufe == parsed.Cufe);
            if (alreadyExists)
            {
                return BadRequest("Ya existe un documento recibido con ese CUFE/CUDE para este emisor.");
            }

            var received = new ReceivedDocument
            {
                Id = Guid.NewGuid(),
                ClientId = clientId,
                BranchId = GetCurrentBranchId(),
                SourceType = "Manual",
                RawXml = xml,
                Cufe = parsed.Cufe,
                DocumentTypeCode = parsed.DocumentTypeCode,
                IssuerTaxId = parsed.IssuerTaxId,
                IssuerName = parsed.IssuerName,
                DocumentId = parsed.DocumentId,
                IssueDate = parsed.IssueDate,
                TotalAmount = parsed.TotalAmount,
                ReceivedAt = DateTime.UtcNow
            };

            _dbContext.ReceivedDocuments.Add(received);
            await _dbContext.SaveChangesAsync();

            await _eventService.TriggerEnabledEventsAsync(received, client);

            return Ok(new { received.Id, received.Cufe, received.IssuerName, received.TotalAmount });
        }

        /// <summary>
        /// Dispara manualmente un evento RADIAN puntual sobre un documento ya recibido — para los
        /// eventos que el Client no tenga automatizados (ej. Reclamo), o para reintentar uno rechazado.
        /// </summary>
        [HttpPost("{id:guid}/events/{eventCode}")]
        public async Task<IActionResult> TriggerEvent(Guid id, string eventCode)
        {
            if (eventCode is not ("030" or "031" or "032" or "033"))
                return BadRequest("Código de evento inválido. Valores permitidos: 030, 031, 032, 033.");

            Guid clientId;
            try
            {
                clientId = GetCurrentClientId();
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(ex.Message);
            }

            var received = await _dbContext.ReceivedDocuments.ForBranch(CurrentBranchScope).FirstOrDefaultAsync(d => d.Id == id && d.ClientId == clientId);
            if (received == null) return NotFound();

            var client = await _dbContext.Clients.FirstOrDefaultAsync(c => c.Id == clientId);
            if (client == null) return Unauthorized(new { message = "Emisor no encontrado." });

            var evt = await _eventService.TriggerEventAsync(received, client, eventCode);
            return Ok(new { evt.EventCode, evt.Status, evt.Cude, evt.DianResponseMessage });
        }

        private static async Task<string> ExtractXmlAsync(IFormFile file)
        {
            using var stream = new MemoryStream();
            await file.CopyToAsync(stream);
            stream.Position = 0;

            var isZip = file.FileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase);
            if (!isZip)
            {
                // Detecta ZIP por firma de archivo aunque venga con otra extensión.
                var header = new byte[4];
                stream.Read(header, 0, 4);
                stream.Position = 0;
                isZip = header[0] == 0x50 && header[1] == 0x4B;
            }

            if (!isZip)
            {
                using var reader = new StreamReader(stream, System.Text.Encoding.UTF8);
                return await reader.ReadToEndAsync();
            }

            using var zip = new ZipArchive(stream, ZipArchiveMode.Read);
            var xmlEntry = zip.Entries.FirstOrDefault(e => e.Name.EndsWith(".xml", StringComparison.OrdinalIgnoreCase));
            if (xmlEntry == null)
                throw new InvalidOperationException("El ZIP no contiene ningún archivo .xml.");

            using var entryStream = xmlEntry.Open();
            using var entryReader = new StreamReader(entryStream, System.Text.Encoding.UTF8);
            return await entryReader.ReadToEndAsync();
        }
    }
}
