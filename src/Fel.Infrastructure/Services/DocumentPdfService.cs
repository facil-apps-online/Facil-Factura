using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Fel.Core.Entities;
using Fel.Core.Interfaces;
using Fel.Infrastructure.Data;
using Fel.Infrastructure.Dataico;
using Microsoft.EntityFrameworkCore;

namespace Fel.Infrastructure.Services
{
    // PDF de un documento ya emitido (factura, notas y equivalente POS) con la misma plantilla y los mismos datos que usa el portal en su vista previa.
    // Lo usa la API de integración para entregar el PDF por UUID; las plantillas viven en Facil Reports, así que el PDF se genera allí.
    public class DocumentPdfService
    {
        private readonly FelDbContext _db;
        private readonly IFacilReportsClient _reports;
        private readonly DocumentLegendService _legends;

        public DocumentPdfService(FelDbContext db, IFacilReportsClient reports, DocumentLegendService legends)
        {
            _db = db;
            _reports = reports;
            _legends = legends;
        }

        // Pdf null con su motivo en Error cuando no se puede generar (sin plantilla elegida, sin tipo, o Facil Reports no respondió).
        public async Task<(byte[]? Pdf, string? Error)> RenderAsync(Guid documentId, Guid clientId)
        {
            var document = await _db.Documents.AsNoTracking()
                .Include(d => d.Customer)
                .Include(d => d.Resolution)
                .Include(d => d.Items.OrderBy(i => i.LineNumber)).ThenInclude(i => i.Retentions)
                .Include(d => d.Items.OrderBy(i => i.LineNumber)).ThenInclude(i => i.AdditionalTaxes)
                .Include(d => d.GeneralRetentions)
                .Include(d => d.Observations)
                .FirstOrDefaultAsync(d => d.Id == documentId && d.ClientId == clientId);
            if (document == null) return (null, "El documento no existe para este emisor.");
            if (!document.DocumentTypeId.HasValue) return (null, "El documento no tiene un tipo asignado.");
            if (document.TypeCode is "DS" or "DS-AJUSTE" || document.TypeCode.StartsWith("NE", StringComparison.OrdinalIgnoreCase))
                return (null, "El PDF de este tipo de documento todavía no está disponible por la API.");

            var client = await _db.Clients.AsNoTracking().Include(c => c.Integrator).FirstOrDefaultAsync(c => c.Id == clientId);
            if (client == null) return (null, "Emisor no encontrado.");

            var template = await DocumentTemplateResolver.ResolveAsync(_db, client, document.DocumentTypeId.Value);
            if (template == null) return (null, "Falta seleccionar la plantilla de esta clase de documento en Diseño de mis Facturas del portal.");

            Document? original = document.ReferenceDocumentId.HasValue
                ? await _db.Documents.AsNoTracking().FirstOrDefaultAsync(d => d.Id == document.ReferenceDocumentId.Value)
                : null;

            var paymentMeans = await _db.TaxCatalogItems.AsNoTracking().Where(i => i.Kind == TaxCatalogKind.PaymentMeans).ToDictionaryAsync(i => i.Category, i => i.Name);
            var formaPago = await _db.TaxCatalogItems.AsNoTracking().Where(i => i.Kind == TaxCatalogKind.FormaPago).ToDictionaryAsync(i => i.Category, i => i.Name);
            var location = await BranchProvisioning.LocationAsync(_db, client, document.BranchId);
            var legend = await _legends.ResolveAsync(client, document.Resolution, document.TypeCode);

            var data = InvoiceReportDataMapper.Build(document, document.Customer, client, location, document.Resolution, document.Items.ToList(), original,
                template.MostrarRetenciones, paymentMeans, formaPago, legend,
                currencyWords: await CurrencyWords.LoadAsync(_db), retentionShortNames: await RetentionShortNameCatalog.LoadAsync(_db));
            if (await InvoiceAnnulment.IsAnnulledAsync(_db, document)) data[InvoiceAnnulment.WatermarkKey] = InvoiceAnnulment.WatermarkText;

            var pdf = await _reports.GenerateReportAsync(template.RepxTemplateKey, data);
            return pdf == null ? (null, "No se pudo generar el PDF en este momento. Intenta de nuevo en unos segundos.") : (pdf, null);
        }
    }
}
