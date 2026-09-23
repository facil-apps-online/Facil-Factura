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
            Func<Dictionary<string, object?>> buildReportData, DataicoCredentials credentials)
        {
            if (string.IsNullOrEmpty(document.Cufe) || !document.DocumentTypeId.HasValue) return;
            if (string.IsNullOrEmpty(recipientEmail)) return;

            try
            {
                var setting = await _dbContext.ClientDocumentSettings
                    .Include(s => s.SelectedTemplate)
                    .FirstOrDefaultAsync(s => s.ClientId == client.Id && s.DocumentTypeId == document.DocumentTypeId);

                if (setting?.SelectedTemplate == null || setting.SelectedTemplate.Status != TemplateStatus.Published) return;

                var data = buildReportData();
                var pdfBytes = await _facilReportsClient.GenerateReportAsync(setting.SelectedTemplate.RepxTemplateKey, data);
                if (pdfBytes == null) return;

                var pdfResult = await _dataicoApiService.SendCustomDocumentPdfAsync(document.Cufe, document.TypeCode, pdfBytes, recipientEmail!, credentials);
                if (pdfResult.Success)
                {
                    document.UsedTemplateId = setting.SelectedTemplate.Id;
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
    }
}
