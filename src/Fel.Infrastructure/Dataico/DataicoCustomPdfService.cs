using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Fel.Core.Entities;
using Fel.Core.Interfaces;
using Fel.Infrastructure.Data;
using Fel.Infrastructure.Dataico.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Fel.Infrastructure.Dataico
{
    // Paso 2 de "documentos personalizados" (Factura, Documento Soporte y Nómina): si el Client
    // tiene una plantilla propia publicada para el tipo de documento, renderiza el PDF vía Facil
    // Reports y se lo reenvía a Dataico para que despache el correo con ese diseño (send_dian=false,
    // send_email=true) en vez del genérico de Dataico. Es una mejora sobre el Paso 1, que ya emitió
    // el documento con éxito — un fallo aquí no debe tumbar la emisión.
    //
    // Antes vivía duplicado dentro de DataicoSubmissionProvider (solo para Factura); se extrajo
    // aquí para que SupportDocumentController y PayrollController lo reutilicen sin copiar la
    // lógica, cada uno solo aporta su propio Func para armar el diccionario de datos del reporte.
    public class DataicoCustomPdfService
    {
        private readonly FelDbContext _dbContext;
        private readonly IDataicoApiService _dataicoApiService;
        private readonly IFacilReportsClient _facilReportsClient;
        private readonly ILogger<DataicoCustomPdfService> _logger;

        public DataicoCustomPdfService(
            FelDbContext dbContext, IDataicoApiService dataicoApiService,
            IFacilReportsClient facilReportsClient, ILogger<DataicoCustomPdfService> logger)
        {
            _dbContext = dbContext;
            _dataicoApiService = dataicoApiService;
            _facilReportsClient = facilReportsClient;
            _logger = logger;
        }

        // Devuelve un aviso solo cuando el documento lleva adjuntos y el correo no pudo salir (el documento ya está emitido: el fallo no lo tumba); sin
        // adjuntos el comportamiento es el de siempre y devuelve null.
        public async Task<string?> TrySendCustomPdfAsync(
            Document document, string? recipientEmail, Client client,
            Func<DocumentTemplate, Dictionary<string, object?>> buildReportData, DataicoCredentials credentials)
        {
            if (string.IsNullOrEmpty(document.DataicoDocumentId) || !document.DocumentTypeId.HasValue) return null;

            // Solo la factura y sus notas llevan adjuntos (DS y nómina no los documentan en Dataico).
            var attachmentService = new Fel.Infrastructure.Services.DocumentAttachmentService(_dbContext);
            var attachments = document.TypeCode is "DS" or "DS-AJUSTE" || document.TypeCode.StartsWith("NE", StringComparison.OrdinalIgnoreCase)
                ? new List<(string Name, byte[] Content)>()
                : await attachmentService.LoadForSendAsync(document.Id);
            var withAttachments = attachments.Count > 0;

            if (string.IsNullOrEmpty(recipientEmail))
                return withAttachments ? "El documento se emitió, pero no se envió el correo con los adjuntos porque el tercero no tiene correo. Agrégaselo y reenvía el documento." : null;
            if (!await IsEmailEnabledAsync(client.Id, document.DocumentTypeId, document.TypeCode))
                return withAttachments ? "El documento se emitió, pero no se envió el correo con los adjuntos porque el envío por correo está apagado para este tipo de documento." : null;

            try
            {
                var (pdfBytes, template) = await ResolveCustomPdfAsync(document, client, buildReportData);
                if (pdfBytes == null || template == null) return null;

                var pdfResult = await _dataicoApiService.SendCustomDocumentPdfAsync(document.DataicoDocumentId!, document.TypeCode, pdfBytes, recipientEmail!, credentials, attachments);
                if (pdfResult.Success)
                {
                    document.UsedTemplateId = template.Id;
                    return null;
                }

                _logger.LogWarning("Dataico rechazó el PDF personalizado del documento {DocumentId} (CUFE {Cufe}): {Detail}", document.Id, document.Cufe, pdfResult.ErrorMessage ?? pdfResult.RawResponse);
                return withAttachments
                    ? $"El documento se emitió, pero el correo con los adjuntos no salió: {DetailOf(pdfResult)}. Puedes reenviarlo desde el detalle."
                    : null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generando/enviando el PDF personalizado del documento {DocumentId}.", document.Id);
                return withAttachments ? "El documento se emitió, pero hubo un error al enviar el correo con los adjuntos. Puedes reenviarlo desde el detalle." : null;
            }
        }

        // Texto corto del motivo por el que Dataico no aceptó el correo (su mensaje si lo dio; si no, el estado).
        private static string DetailOf(DataicoResult result)
        {
            var text = string.IsNullOrWhiteSpace(result.ErrorMessage) ? result.RawResponse : result.ErrorMessage;
            text = string.IsNullOrWhiteSpace(text) ? $"respuesta {result.StatusCode}" : text.Trim();
            return text.Length > 300 ? text[..300] + "…" : text;
        }

        // Interruptor por cliente y tipo de documento (ClientEnabledDocumentType.SendEmailToRecipient).
        // Si el tipo no está en la lista del cliente no hay interruptor guardado: aplica el valor por defecto del tipo.
        private async Task<bool> IsEmailEnabledAsync(Guid clientId, Guid? documentTypeId, string? typeCode)
        {
            var flag = await _dbContext.ClientEnabledDocumentTypes
                .Where(e => e.ClientId == clientId && e.DocumentTypeId == documentTypeId)
                .Select(e => (bool?)e.SendEmailToRecipient)
                .FirstOrDefaultAsync();
            return flag ?? ClientEnabledDocumentType.DefaultSendEmail(typeCode);
        }

        // Antes de emitir por Dataico el cliente debe haber elegido y publicado la plantilla de ese tipo de documento: sin ella el correo
        // saldría con el diseño predeterminado de Dataico. No aplica si ese tipo no envía correo (la nómina, por defecto), porque
        // entonces el PDF propio no se usa.
        public async Task<bool> IsPublishedTemplateMissingAsync(Guid clientId, Guid? documentTypeId, string? typeCode)
        {
            if (!documentTypeId.HasValue) return false;
            if (!await IsEmailEnabledAsync(clientId, documentTypeId, typeCode)) return false;
            var hasTemplate = await _dbContext.ClientDocumentSettings
                .AnyAsync(s => s.ClientId == clientId && s.DocumentTypeId == documentTypeId
                    && s.SelectedTemplate != null && s.SelectedTemplate.Status == TemplateStatus.Published);
            return !hasTemplate;
        }

        // Extraído para que el reenvío manual (InvoiceController.Resend) pueda decidir si manda un
        // PDF propio o deja que Dataico reenvíe con el suyo, con el mismo criterio que el paso 2
        // automático: solo si el Client tiene una plantilla explícitamente seleccionada y publicada
        // para este tipo de documento (no la resolución "más específica disponible" que usa /preview).
        public async Task<(byte[]? PdfBytes, DocumentTemplate? Template)> ResolveCustomPdfAsync(
            Document document, Client client, Func<DocumentTemplate, Dictionary<string, object?>> buildReportData)
        {
            if (!document.DocumentTypeId.HasValue) return (null, null);

            var setting = await _dbContext.ClientDocumentSettings
                .Include(s => s.SelectedTemplate)
                .FirstOrDefaultAsync(s => s.ClientId == client.Id && s.DocumentTypeId == document.DocumentTypeId);

            if (setting?.SelectedTemplate == null || setting.SelectedTemplate.Status != TemplateStatus.Published) return (null, null);

            var data = buildReportData(setting.SelectedTemplate);
            var pdfBytes = await _facilReportsClient.GenerateReportAsync(setting.SelectedTemplate.RepxTemplateKey, data);
            return (pdfBytes, setting.SelectedTemplate);
        }
    }
}
