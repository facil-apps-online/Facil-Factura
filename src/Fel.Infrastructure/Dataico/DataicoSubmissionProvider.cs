using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Fel.Core.Entities;
using Fel.Core.Interfaces;
using Fel.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Fel.Infrastructure.Services;

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
        private readonly Fel.Infrastructure.Services.DocumentLegendService _legendService;

        public string IntegratorCode => "DATAICO";

        private static string? ExternalOriginalId(Document invoice)
        {
            if (string.IsNullOrWhiteSpace(invoice.SectorExtensionData)) return null;
            try
            {
                using var json = JsonDocument.Parse(invoice.SectorExtensionData);
                return json.RootElement.TryGetProperty("dataicoExternalReference", out var reference)
                    && reference.TryGetProperty("dataicoDocumentId", out var id)
                    ? id.GetString() : null;
            }
            catch (JsonException) { return null; }
        }

        public DataicoSubmissionProvider(
            IDataicoApiService dataicoApiService, ICryptoService cryptoService, FelDbContext dbContext,
            DataicoCustomPdfService customPdfService, Fel.Infrastructure.Services.DocumentLegendService legendService)
        {
            _dataicoApiService = dataicoApiService;
            _cryptoService = cryptoService;
            _dbContext = dbContext;
            _customPdfService = customPdfService;
            _legendService = legendService;
        }

        public async Task<DocumentSubmissionResult> SubmitAsync(
            Document invoice, Customer customer, ICollection<DocumentItem> items,
            Client client, Resolution resolution, string paymentMeans, string paymentMeansType)
        {
            // Con Dataico se emiten factura estándar, de mandato, de exportación, AIU y notas crédito/débito: los demás tipos de
            // factura (salud, transporte) llevan información propia que este envío no manda, y saldrían como
            // una venta común. Solo están disponibles con emisión directa a la DIAN.
            var isMandate = false;
            var isExport = false;
            Fel.Core.Models.AiuContractData? aiu = null;
            if (invoice.DocumentTypeId.HasValue)
            {
                var documentType = await _dbContext.DocumentTypes.AsNoTracking()
                    .Where(t => t.Id == invoice.DocumentTypeId.Value).Select(t => new { t.Code, t.OperationType }).FirstOrDefaultAsync();
                if (documentType != null && documentType.Code is not ("FE-STD" or "FE-MANDATO" or "FE-EXP" or "FE-AIU" or "NC" or "ND"))
                {
                    throw new NotSupportedException("Este tipo de factura solo está disponible con emisión directa a la DIAN; con el integrador configurado solo se emite factura estándar, de mandato, de exportación, AIU y notas crédito/débito.");
                }
                if (documentType?.Code == "FE-AIU")
                {
                    // Mismas exigencias que la vía directa: el objeto del contrato y las tres líneas A, I y U.
                    aiu = Fel.Core.Models.AiuContractData.TryRead(invoice.SectorExtensionData);
                    if (aiu == null || string.IsNullOrWhiteSpace(aiu.ContractObject))
                        throw new NotSupportedException("La factura AIU debe indicar el objeto del contrato.");
                    var codes = items.Select(i => i.Code).ToHashSet();
                    // Dataico pide además la línea del servicio contratado: un borrador AIU creado antes de que existiera no la tiene.
                    if (!codes.Contains(Fel.Core.Models.AiuContractData.ServiceCode))
                        throw new NotSupportedException("La factura AIU debe llevar la línea del servicio contratado. Abre el borrador y vuelve a indicar el valor del servicio para regenerar las líneas.");
                    if (!codes.Contains(Fel.Core.Models.AiuContractData.AdminCode) || !codes.Contains(Fel.Core.Models.AiuContractData.UnforeseenCode) || !codes.Contains(Fel.Core.Models.AiuContractData.ProfitCode))
                        throw new NotSupportedException("La factura AIU debe llevar las líneas de Administración, Imprevistos y Utilidad.");
                }
                isMandate = documentType?.OperationType == DocumentType.MandateOperationType;
                isExport = documentType?.Code == "FE-EXP";
            }

            var credentials = DataicoDocumentMapper.ToCredentials(client, _cryptoService);

            if (string.IsNullOrEmpty(invoice.Number) && invoice.TypeCode is not ("NC" or "ND"))
            {
                // Dataico puede devolver como exitosa la factura que ya existe cuando se repite el
                // mismo prefijo y número. Verificamos antes de reclamarlo para no reutilizar una
                // numeración que Dataico ya tiene ocupada.
                for (var attempt = 0; attempt < 100; attempt++)
                {
                    var candidate = await Fel.Infrastructure.Services.ResolutionNumbering.PeekNextNumberAsync(_dbContext, resolution.Id);
                    var existing = await _dataicoApiService.GetInvoiceByNumberAsync($"{resolution.Prefix}{candidate}", credentials);
                    var existsInDataico = existing.Success &&
                        (!string.IsNullOrWhiteSpace(existing.DataicoDocumentId) ||
                         !string.IsNullOrWhiteSpace(existing.Cufe) ||
                         !string.IsNullOrWhiteSpace(existing.DocumentNumber));
                    if (existsInDataico)
                    {
                        await Fel.Infrastructure.Services.ResolutionNumbering.CommitNumberAsync(_dbContext, resolution.Id, candidate);
                        continue;
                    }

                    var claimed = await Fel.Infrastructure.Services.ResolutionNumbering.ClaimNextNumberAsync(_dbContext, resolution.Id);
                    if (claimed == candidate)
                    {
                        invoice.Number = candidate.ToString();
                        break;
                    }
                }
                if (string.IsNullOrEmpty(invoice.Number))
                    throw new InvalidOperationException("No se pudo encontrar un consecutivo disponible en Dataico.");
            }
            else if (string.IsNullOrEmpty(invoice.Number))
            {
                // Las Notas Crédito/Débito no tienen rango autorizado propio ante la DIAN, así que
                // no pueden compartir el NextNumber de la resolución de Factura — antes lo hacían,
                // y cada nota consumía un número que le correspondía a la siguiente factura real.
                invoice.Number = invoice.TypeCode switch
                {
                    "NC" => (await Fel.Infrastructure.Services.ResolutionNumbering.ClaimNextNoteAsync(_dbContext, client.Id, invoice.BranchId, Fel.Core.Entities.NoteKind.CreditNote, resolution.Id)).Number.ToString(),
                    "ND" => (await Fel.Infrastructure.Services.ResolutionNumbering.ClaimNextNoteAsync(_dbContext, client.Id, invoice.BranchId, Fel.Core.Entities.NoteKind.DebitNote, resolution.Id)).Number.ToString(),
                    _ => (await Fel.Infrastructure.Services.ResolutionNumbering.ClaimNextNumberAsync(_dbContext, resolution.Id)).ToString()
                };
            }
            // El prefijo queda guardado en el documento junto con su consecutivo.
            // Las notas pueden llevar el prefijo de su propia numeración (sucursal con contador propio); sin uno usan el de la resolución.
            invoice.Prefix = invoice.TypeCode switch
            {
                "NC" => resolution.Prefix,
                "ND" => resolution.Prefix,
                _ => resolution.Prefix
            };

            Fel.Infrastructure.Dataico.Models.DataicoResult result;
            Document? originalInvoiceForPdf = null;
            var externalOriginalId = ExternalOriginalId(invoice);

            if (invoice.TypeCode == "NC" && !invoice.ReferenceDocumentId.HasValue && string.IsNullOrWhiteSpace(externalOriginalId))
            {
                // Nota crédito sin factura de referencia: lleva su propio tercero, los ítems y el motivo; el período es el mes de la emisión.
                if (customer == null) throw new NotSupportedException("La nota crédito sin factura debe indicar el tercero.");
                if (items.Count == 0) throw new NotSupportedException("La nota crédito sin factura debe llevar al menos un ítem.");
                if (string.IsNullOrWhiteSpace(invoice.DiscrepancyResponseCode)) throw new NotSupportedException("La nota crédito sin factura debe indicar el motivo.");

                var reasonCodes = await _dbContext.TaxCatalogItems.AsNoTracking()
                    .Where(t => t.Kind == TaxCatalogKind.CreditNoteReason && t.DianCode != null)
                    .ToDictionaryAsync(t => t.Category, t => t.DianCode!);
                var idOverrides = await _dbContext.IdentificationTypes
                    .Where(t => t.DataicoCode != null && t.DataicoCode != "")
                    .ToDictionaryAsync(t => t.Code, t => t.DataicoCode!);
                result = await _dataicoApiService.SendCreditNoteAsync(
                    DataicoDocumentMapper.BuildCreditNoteRequest(invoice, resolution, client, null, items, reasonCodes, customer, idOverrides),
                    credentials);
            }
            else if (invoice.TypeCode is "NC" or "ND")
            {
                if (!invoice.ReferenceDocumentId.HasValue)
                {
                    throw new NotSupportedException("Esta nota debe referenciar la factura que ajusta.");
                }

                var originalInvoice = invoice.ReferenceDocumentId.HasValue
                    ? await _dbContext.Documents.FindAsync(invoice.ReferenceDocumentId.Value)
                    : null;
                originalInvoiceForPdf = originalInvoice;
                var originalDataicoId = originalInvoice?.DataicoDocumentId ?? externalOriginalId;
                if (string.IsNullOrEmpty(originalDataicoId))
                {
                    throw new NotSupportedException("La factura original no tiene un identificador de Dataico registrado (puede no haberse emitido por Dataico, o haberse emitido antes de que empezáramos a guardarlo). No se puede generar la nota.");
                }

                var reasonKind = invoice.TypeCode == "NC" ? TaxCatalogKind.CreditNoteReason : TaxCatalogKind.DebitNoteReason;
                var reasonDianCodes = await _dbContext.TaxCatalogItems.AsNoTracking()
                    .Where(t => t.Kind == reasonKind && t.DianCode != null)
                    .ToDictionaryAsync(t => t.Category, t => t.DianCode!);

                result = invoice.TypeCode == "NC"
                    ? await _dataicoApiService.SendCreditNoteAsync(
                        DataicoDocumentMapper.BuildCreditNoteRequest(invoice, resolution, client, originalDataicoId, items, reasonDianCodes),
                        credentials)
                    : await _dataicoApiService.SendDebitNoteAsync(
                        DataicoDocumentMapper.BuildDebitNoteRequest(invoice, resolution, client, originalDataicoId, items, reasonDianCodes),
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

                var dataicoRequest = DataicoDocumentMapper.BuildInvoiceRequest(invoice, customer, items, resolution, client, paymentMeans, paymentMeansType, identificationTypeOverrides, isMandate, isExport, aiu);
                result = await _dataicoApiService.SendInvoiceAsync(dataicoRequest, credentials);

                // Una respuesta 2xx no basta: si Dataico devolviera el documento anterior,
                // sus datos deben corresponder al adquirente de esta factura.
                if (result.Success && !string.IsNullOrWhiteSpace(result.CustomerIdentification) &&
                    !SameIdentification(result.CustomerIdentification, customer?.IdentificationNumber))
                {
                    result.Success = false;
                    result.ErrorMessage = "Dataico devolvió una factura existente con un adquirente diferente al de esta factura.";
                }
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
                var leyenda = await _legendService.ResolveAsync(client, resolution, invoice.TypeCode);
                var currencyWords = await CurrencyWords.LoadAsync(_dbContext);
                var location = await BranchProvisioning.LocationAsync(_dbContext, client, invoice.BranchId);
                await _customPdfService.TrySendCustomPdfAsync(
                    invoice, customer?.Email, client,
                    template => InvoiceReportDataMapper.Build(invoice, customer, client, location, resolution, items.ToList(), originalInvoiceForPdf, template.MostrarRetenciones, leyenda: leyenda, currencyWords: currencyWords),
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

        private static bool SameIdentification(string? left, string? right) =>
            new string((left ?? string.Empty).Where(char.IsDigit).ToArray()) ==
            new string((right ?? string.Empty).Where(char.IsDigit).ToArray());
    }
}
