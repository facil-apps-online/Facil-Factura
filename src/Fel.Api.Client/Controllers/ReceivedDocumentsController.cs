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
using Fel.Infrastructure.Dataico;

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
        private readonly IDataicoApiService _dataicoApiService;
        private readonly ICryptoService _cryptoService;

        public ReceivedDocumentsController(FelDbContext dbContext, IReceptionEventService eventService, IDataicoApiService dataicoApiService, ICryptoService cryptoService)
        {
            _dbContext = dbContext;
            _eventService = eventService;
            _dataicoApiService = dataicoApiService;
            _cryptoService = cryptoService;
        }

        // Un documento soporte trae InvoiceTypeCode = 05. Se pregunta en la base de datos para no traer el XML completo de cada documento (decenas de KB).
        private const string SupportDocumentXmlPattern = "%InvoiceTypeCode%>05</%InvoiceTypeCode>%";

        // Solo lo que muestra la lista. El XML, el PDF original y las respuestas técnicas de los eventos no viajan: el historial de un evento se pide al abrirlo.
        [HttpGet]
        public async Task<IActionResult> GetReceivedDocuments()
        {
            try
            {
                var clientId = GetCurrentClientId();
                var documents = await _dbContext.ReceivedDocuments.AsNoTracking().ForBranch(CurrentBranchScope)
                    .Where(d => d.ClientId == clientId)
                    .OrderByDescending(d => d.ReceivedAt)
                    .Select(d => new
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
                        d.IsCredit,
                        IsSupportDocument = d.DocumentTypeCode == "05" || EF.Functions.Like(d.RawXml, SupportDocumentXmlPattern),
                        d.ReceivedAt,
                        // El mensaje se recorta: en la fila solo se ve como ayuda al pasar el cursor; completo está en el historial del evento.
                        Events = d.Events.OrderByDescending(e => e.SentAt).Select(e => new
                        {
                            e.Id, e.EventCode, e.Status, e.SentAt, e.EventNumber,
                            DianResponseMessage = e.DianResponseMessage == null ? null : e.DianResponseMessage.Substring(0, 300)
                        })
                    })
                    .ToListAsync();

                return Ok(documents);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(ex.Message);
            }
        }

        // Historial completo de los eventos de un documento recibido (respuestas técnicas y enlaces de Dataico): se pide al abrir el historial.
        [HttpGet("{id:guid}/events")]
        public async Task<IActionResult> GetEvents(Guid id)
        {
            try
            {
                var clientId = GetCurrentClientId();
                var events = await _dbContext.ReceivedDocumentEvents.AsNoTracking()
                    .Where(e => e.ReceivedDocumentId == id && e.ReceivedDocument.ClientId == clientId)
                    .OrderByDescending(e => e.SentAt)
                    .Select(e => new { e.Id, e.EventCode, e.Status, e.SentAt, e.EventNumber, e.ProviderUid, e.Cude, e.DianResponseMessage, e.ProviderResponse, e.ProviderXmlUrl, e.ProviderPdfUrl })
                    .ToListAsync();
                return Ok(events);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(ex.Message);
            }
        }

        [HttpGet("{id:guid}/events/{eventId:guid}/file")]
        public async Task<IActionResult> DownloadEventFile(Guid id, Guid eventId, [FromQuery] string type)
        {
            var clientId = GetCurrentClientId();
            var eventRow = await _dbContext.ReceivedDocumentEvents
                .Include(e => e.ReceivedDocument)
                .ThenInclude(d => d.Client)
                .ThenInclude(c => c.Integrator)
                .Where(e => e.Id == eventId && e.ReceivedDocumentId == id && e.ReceivedDocument.ClientId == clientId)
                .FirstOrDefaultAsync();
            if (eventRow == null) return NotFound();
            if (!string.Equals(eventRow.ReceivedDocument.Client.Integrator?.Code, "DATAICO", StringComparison.OrdinalIgnoreCase)) return BadRequest("El archivo del evento solo está disponible para Dataico.");

            var url = string.Equals(type, "xml", StringComparison.OrdinalIgnoreCase) ? eventRow.ProviderXmlUrl :
                string.Equals(type, "pdf", StringComparison.OrdinalIgnoreCase) ? eventRow.ProviderPdfUrl : null;
            if (string.IsNullOrWhiteSpace(url)) return NotFound(new { message = "Dataico no entregó este archivo." });
            var file = await _dataicoApiService.DownloadFileAsync(url, DataicoDocumentMapper.ToCredentials(eventRow.ReceivedDocument.Client, _cryptoService));
            if (file == null) return StatusCode(502, new { message = "Dataico no permitió descargar este archivo." });
            var extension = string.Equals(type, "xml", StringComparison.OrdinalIgnoreCase) ? "xml" : "pdf";
            return File(file.Value.Bytes, file.Value.ContentType, $"{eventRow.ReceivedDocument.DocumentId}-{eventRow.EventCode}.{extension}");
        }

        [HttpGet("{id:guid}/xml")]
        public async Task<IActionResult> DownloadXml(Guid id)
        {
            var clientId = GetCurrentClientId();
            var document = await _dbContext.ReceivedDocuments.ForBranch(CurrentBranchScope)
                .FirstOrDefaultAsync(d => d.Id == id && d.ClientId == clientId);
            if (document == null) return NotFound();
            return File(System.Text.Encoding.UTF8.GetBytes(document.RawXml), "application/xml", $"{document.DocumentId}.xml");
        }

        [HttpGet("{id:guid}/pdf")]
        public async Task<IActionResult> DownloadPdf(Guid id)
        {
            var clientId = GetCurrentClientId();
            var document = await _dbContext.ReceivedDocuments.ForBranch(CurrentBranchScope)
                .FirstOrDefaultAsync(d => d.Id == id && d.ClientId == clientId);
            if (document == null) return NotFound();
            if (document.OriginalPdf == null || document.OriginalPdf.Length == 0) return NotFound(new { message = "Este documento no tiene representación gráfica almacenada." });
            return File(document.OriginalPdf, "application/pdf", document.OriginalPdfFileName ?? $"{document.DocumentId}.pdf");
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
            byte[]? originalPdf = null;
            string? originalPdfFileName = null;
            try
            {
                var extracted = await ExtractFilesAsync(file);
                xml = extracted.Xml;
                originalPdf = extracted.Pdf;
                originalPdfFileName = extracted.PdfFileName;
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
                OriginalPdf = originalPdf,
                OriginalPdfFileName = originalPdfFileName,
                Cufe = parsed.Cufe,
                DocumentTypeCode = parsed.DocumentTypeCode,
                IssuerTaxId = parsed.IssuerTaxId,
                IssuerName = parsed.IssuerName,
                DocumentId = parsed.DocumentId,
                IssueDate = parsed.IssueDate,
                TotalAmount = parsed.TotalAmount,
                IsCredit = parsed.IsCredit,
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
            if (!received.IsCredit) return BadRequest("Los eventos RADIAN solo aplican a facturas a crédito.");

            var client = await _dbContext.Clients.FirstOrDefaultAsync(c => c.Id == clientId);
            if (client == null) return Unauthorized(new { message = "Emisor no encontrado." });

            var evt = await _eventService.TriggerEventAsync(received, client, eventCode);
            return Ok(new { evt.EventCode, evt.Status, evt.Cude, evt.DianResponseMessage });
        }

        private static async Task<(string Xml, byte[]? Pdf, string? PdfFileName)> ExtractFilesAsync(IFormFile file)
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
                return (ReceivedDocumentXmlExtractor.Extract(await reader.ReadToEndAsync()), null, null);
            }

            using var zip = new ZipArchive(stream, ZipArchiveMode.Read);
            var xmlEntry = zip.Entries.FirstOrDefault(e => e.Name.EndsWith(".xml", StringComparison.OrdinalIgnoreCase));
            if (xmlEntry == null)
                throw new InvalidOperationException("El ZIP no contiene ningún archivo .xml.");

            using var entryStream = xmlEntry.Open();
            using var entryReader = new StreamReader(entryStream, System.Text.Encoding.UTF8);
            var pdfEntry = zip.Entries.FirstOrDefault(e => e.Name.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase));
            byte[]? pdf = null;
            string? pdfName = null;
            if (pdfEntry != null)
            {
                using var pdfStream = new MemoryStream();
                using var pdfEntryStream = pdfEntry.Open();
                await pdfEntryStream.CopyToAsync(pdfStream);
                pdf = pdfStream.ToArray();
                pdfName = Path.GetFileName(pdfEntry.Name);
            }
            return (ReceivedDocumentXmlExtractor.Extract(await entryReader.ReadToEndAsync()), pdf, pdfName);
        }
    }
}
