using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Fel.Core.Entities;
using Fel.Core.Interfaces;
using Fel.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Fel.Infrastructure.Dataico
{
    // Envuelve el envío a Dataico (ya probado en producción) detrás del contrato común de
    // IDocumentSubmissionProvider, para que InvoiceController no distinga integradores.
    //
    // Notas Crédito/Débito van a un endpoint distinto de Dataico (/credit_notes, /debit_notes,
    // no /invoices) y necesitan el "uuid" que Dataico le asignó a la factura original — antes se
    // enviaban siempre como si fueran una factura nueva, sin esa referencia.
    public class DataicoSubmissionProvider : IDocumentSubmissionProvider
    {
        private readonly IDataicoApiService _dataicoApiService;
        private readonly ICryptoService _cryptoService;
        private readonly FelDbContext _dbContext;
        private readonly DataicoCustomPdfService _customPdfService;

        public string IntegratorCode => "DATAICO";

        public DataicoSubmissionProvider(
            IDataicoApiService dataicoApiService, ICryptoService cryptoService, FelDbContext dbContext,
            DataicoCustomPdfService customPdfService)
        {
            _dataicoApiService = dataicoApiService;
            _cryptoService = cryptoService;
            _dbContext = dbContext;
            _customPdfService = customPdfService;
        }

        public async Task<DocumentSubmissionResult> SubmitAsync(
            Document invoice, Customer customer, ICollection<DocumentItem> items,
            Client client, Resolution resolution, string paymentMeans, string paymentMeansType)
        {
            if (string.IsNullOrEmpty(invoice.Number))
            {
                // Las Notas Crédito/Débito no tienen rango autorizado propio ante la DIAN, así que
                // no pueden compartir el NextNumber de la resolución de Factura — antes lo hacían,
                // y cada nota consumía un número que le correspondía a la siguiente factura real.
                invoice.Number = invoice.TypeCode switch
                {
                    "NC" => (await Fel.Infrastructure.Services.ResolutionNumbering.ClaimNextCreditNoteNumberAsync(_dbContext, client.Id)).ToString(),
                    "ND" => (await Fel.Infrastructure.Services.ResolutionNumbering.ClaimNextDebitNoteNumberAsync(_dbContext, client.Id)).ToString(),
                    _ => (await Fel.Infrastructure.Services.ResolutionNumbering.ClaimNextNumberAsync(_dbContext, resolution.Id)).ToString()
                };
            }
            var credentials = DataicoDocumentMapper.ToCredentials(client, _cryptoService);

            Fel.Infrastructure.Dataico.Models.DataicoResult result;
            Document? originalInvoiceForPdf = null;

            if (invoice.TypeCode is "NC" or "ND")
            {
                if (!invoice.ReferenceDocumentId.HasValue)
                {
                    throw new NotSupportedException("Esta nota debe referenciar la factura que ajusta.");
                }

                var originalInvoice = await _dbContext.Documents.FindAsync(invoice.ReferenceDocumentId.Value);
                originalInvoiceForPdf = originalInvoice;
                if (originalInvoice == null || string.IsNullOrEmpty(originalInvoice.DataicoDocumentId))
                {
                    throw new NotSupportedException("La factura original no tiene un identificador de Dataico registrado (puede no haberse emitido por Dataico, o haberse emitido antes de que empezáramos a guardarlo). No se puede generar la nota.");
                }

                var reasonKind = invoice.TypeCode == "NC" ? TaxCatalogKind.CreditNoteReason : TaxCatalogKind.DebitNoteReason;
                var reasonDianCodes = await _dbContext.TaxCatalogItems.AsNoTracking()
                    .Where(t => t.Kind == reasonKind && t.DianCode != null)
                    .ToDictionaryAsync(t => t.Category, t => t.DianCode!);

                result = invoice.TypeCode == "NC"
                    ? await _dataicoApiService.SendCreditNoteAsync(
                        DataicoDocumentMapper.BuildCreditNoteRequest(invoice, resolution, client, originalInvoice.DataicoDocumentId, items, reasonDianCodes),
                        credentials)
                    : await _dataicoApiService.SendDebitNoteAsync(
                        DataicoDocumentMapper.BuildDebitNoteRequest(invoice, resolution, client, originalInvoice.DataicoDocumentId, items, reasonDianCodes),
                        credentials);
            }
            else
            {
                // Equivalencia de tipo de identificación administrable desde el catálogo (Superadmin
                // > Tipos de Identificación); si un código no tiene DataicoCode capturado, ToDataicoParty
                // cae a la tabla fija de DataicoMapper (ya corregida a los valores reales de Dataico).
                var identificationTypeOverrides = await _dbContext.IdentificationTypes
                    .Where(t => t.DataicoCode != null && t.DataicoCode != "")
                    .ToDictionaryAsync(t => t.Code, t => t.DataicoCode!);

                var dataicoRequest = DataicoDocumentMapper.BuildInvoiceRequest(invoice, customer, items, resolution, client, paymentMeans, paymentMeansType, identificationTypeOverrides);
                result = await _dataicoApiService.SendInvoiceAsync(dataicoRequest, credentials);
            }

            invoice.Status = result.Success ? "APPROVED" : "REJECTED";
            invoice.DianResponseMessage = result.Success ? result.RawResponse : (result.ErrorMessage ?? result.RawResponse);
            invoice.Cufe = result.Cufe;
            invoice.QrCode = result.QrCode;
            invoice.DataicoDocumentId = result.DataicoDocumentId;
            invoice.PdfUrl = result.PdfUrl;
            invoice.XmlUrl = result.XmlUrl;
            invoice.ProcessedAt = DateTime.UtcNow;

            if (result.Success)
            {
                await _customPdfService.TrySendCustomPdfAsync(
                    invoice, customer?.Email, client,
                    template => InvoiceReportDataMapper.Build(invoice, customer, client, resolution, items.ToList(), originalInvoiceForPdf, template.MostrarRetenciones),
                    credentials);
            }

            return new DocumentSubmissionResult
            {
                Success = result.Success,
                Status = invoice.Status,
                Cufe = result.Cufe,
                ResponseMessage = invoice.DianResponseMessage
            };
        }
    }
}
