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
                invoice.Number = (await Fel.Infrastructure.Services.ResolutionNumbering.ClaimNextNumberAsync(_dbContext, resolution.Id)).ToString();
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

                result = invoice.TypeCode == "NC"
                    ? await _dataicoApiService.SendCreditNoteAsync(
                        DataicoDocumentMapper.BuildCreditNoteRequest(invoice, resolution, client, originalInvoice.DataicoDocumentId, items),
                        credentials)
                    : await _dataicoApiService.SendDebitNoteAsync(
                        DataicoDocumentMapper.BuildDebitNoteRequest(invoice, resolution, client, originalInvoice.DataicoDocumentId, items),
                        credentials);
            }
            else
            {
                var dataicoRequest = DataicoDocumentMapper.BuildInvoiceRequest(invoice, customer, items, resolution, client, paymentMeans, paymentMeansType);
                result = await _dataicoApiService.SendInvoiceAsync(dataicoRequest, credentials);
            }

            invoice.Status = result.Success ? "APPROVED" : "REJECTED";
            invoice.DianResponseMessage = result.Success ? result.RawResponse : (result.ErrorMessage ?? result.RawResponse);
            invoice.Cufe = result.Cufe;
            invoice.DataicoDocumentId = result.DataicoDocumentId;
            invoice.PdfUrl = result.PdfUrl;
            invoice.XmlUrl = result.XmlUrl;
            invoice.ProcessedAt = DateTime.UtcNow;

            if (result.Success)
            {
                await _customPdfService.TrySendCustomPdfAsync(
                    invoice, customer?.Email, client,
                    () => InvoiceReportDataMapper.Build(invoice, customer, client, resolution, items.ToList(), originalInvoiceForPdf),
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
