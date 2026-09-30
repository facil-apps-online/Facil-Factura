using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Fel.Core.Entities;
using Fel.Core.Interfaces;
using Fel.Infrastructure.Data;
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

        public async Task TrySendCustomPdfAsync(
            Document document, string? recipientEmail, Client client,
            Func<DocumentTemplate, Dictionary<string, object?>> buildReportData, DataicoCredentials credentials)
        {
            if (string.IsNullOrEmpty(document.DataicoDocumentId) || !document.DocumentTypeId.HasValue) return;
            if (string.IsNullOrEmpty(recipientEmail)) return;

            try
            {
                var (pdfBytes, template) = await ResolveCustomPdfAsync(document, client, buildReportData);
                if (pdfBytes == null || template == null) return;

                var pdfResult = await _dataicoApiService.SendCustomDocumentPdfAsync(document.DataicoDocumentId!, document.TypeCode, pdfBytes, recipientEmail!, credentials);
                if (pdfResult.Success)
                {
                    document.UsedTemplateId = template.Id;
                }
                else
                {
                    _logger.LogWarning("Dataico rechazó el PDF personalizado del documento {DocumentId} (CUFE {Cufe}): {Detail}", document.Id, document.Cufe, pdfResult.ErrorMessage ?? pdfResult.RawResponse);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generando/enviando el PDF personalizado del documento {DocumentId}.", document.Id);
            }
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
