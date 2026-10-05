using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Fel.Core.Entities;
using Fel.Core.Interfaces;
using Fel.Infrastructure.Data;
using Fel.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Fel.Infrastructure.Dian
{
    // Emisión directa a la DIAN vía certificado propio del cliente, para el portal de cliente.
    // A diferencia de Fel.Worker (que atiende la cola del endpoint B2B y crea su propio Document),
    // este proveedor opera sobre el Document que ya existe, igual que DataicoSubmissionProvider.
    //
    // Requiere Client.CityCode y el catálogo DianMunicipalities poblados para pasar la validación
    // real de la DIAN (ver DianDocumentMapper); mientras tanto arma y firma el XML igual, solo que
    // con ciudad/departamento vacíos, y SendBillAsync es asíncrono del lado de la DIAN — el
    // resultado de esta llamada solo confirma que el envío llegó, no que fue aceptado (falta
    // consumir GetStatus para eso), así que el documento queda en PROCESSING, no APPROVED.
    public class NativeDianSubmissionProvider : IDocumentSubmissionProvider
    {
        private readonly FelDbContext _dbContext;
        private readonly IUblGenerator _ublGenerator;
        private readonly IXmlSigner _xmlSigner;
        private readonly ICryptoVault _cryptoVault;
        private readonly IDianSoapClient _dianSoapClient;
        private readonly ILogger<NativeDianSubmissionProvider> _logger;

        public string IntegratorCode => "NATIVE";

        public NativeDianSubmissionProvider(
            FelDbContext dbContext, IUblGenerator ublGenerator, IXmlSigner xmlSigner,
            ICryptoVault cryptoVault, IDianSoapClient dianSoapClient, ILogger<NativeDianSubmissionProvider> logger)
        {
            _dbContext = dbContext;
            _ublGenerator = ublGenerator;
            _xmlSigner = xmlSigner;
            _cryptoVault = cryptoVault;
            _dianSoapClient = dianSoapClient;
            _logger = logger;
        }

        public async Task<DocumentSubmissionResult> SubmitAsync(
            Document invoice, Customer customer, ICollection<DocumentItem> items,
            Client client, Resolution resolution, string paymentMeans, string paymentMeansType)
        {
            var certificate = await _dbContext.Certificates
                .FirstOrDefaultAsync(c => c.ClientId == client.Id && c.IsActive);
            if (certificate == null)
            {
                throw new NotSupportedException("Este emisor no tiene un certificado digital activo cargado para facturar directo a la DIAN.");
            }

            X509Certificate2 cert;
            try
            {
                cert = _cryptoVault.GetCertificate(certificate.FileName, certificate.EncryptedPassword);
            }
            catch (Exception ex)
            {
                invoice.Status = "REJECTED";
                invoice.DianResponseMessage = $"No se pudo cargar el certificado: {ex.Message}";
                invoice.ProcessedAt = DateTime.UtcNow;
                return new DocumentSubmissionResult { Success = false, Status = invoice.Status, Cufe = string.Empty, ResponseMessage = invoice.DianResponseMessage };
            }

            // Doble verificación: en esta arquitectura el Client siempre factura con su propio NIT,
            // nunca a nombre de otro — si el certificado cargado no es del mismo titular que el
            // Client registrado, es un error de datos (certificado equivocado, o Client mal
            // registrado) y no se debe firmar nada con esa inconsistencia.
            var certificateNit = _cryptoVault.ExtractNit(cert);
            if (!NitValidation.Matches(certificateNit, client.TaxId))
            {
                invoice.Status = "REJECTED";
                invoice.DianResponseMessage = $"El NIT del certificado ({certificateNit ?? "no encontrado"}) no coincide con el NIT registrado del emisor ({client.TaxId}).";
                invoice.ProcessedAt = DateTime.UtcNow;
                return new DocumentSubmissionResult { Success = false, Status = invoice.Status, Cufe = string.Empty, ResponseMessage = invoice.DianResponseMessage };
            }

            var documentType = invoice.DocumentTypeId.HasValue
                ? await _dbContext.DocumentTypes.AsNoTracking().FirstOrDefaultAsync(t => t.Id == invoice.DocumentTypeId.Value)
                : null;

            // Contrato AIU (operación 09): el Anexo exige el objeto del contrato en la línea de
            // Administración; sin él (o sin las tres líneas) la DIAN rechazaría el documento, así que se
            // frena acá antes de reclamar un consecutivo.
            if (documentType?.OperationType == "09")
            {
                var aiu = Fel.Core.Models.AiuContractData.TryRead(invoice.SectorExtensionData);
                if (aiu == null || string.IsNullOrWhiteSpace(aiu.ContractObject))
                    throw new NotSupportedException("La factura AIU debe indicar el objeto del contrato (es obligatorio en la línea de Administración).");
                var codes = items.Select(i => i.Code).ToHashSet();
                if (!codes.Contains(Fel.Core.Models.AiuContractData.AdminCode) || !codes.Contains(Fel.Core.Models.AiuContractData.UnforeseenCode) || !codes.Contains(Fel.Core.Models.AiuContractData.ProfitCode))
                    throw new NotSupportedException("La factura AIU debe llevar las líneas de Administración, Imprevistos y Utilidad.");
            }

            if (string.IsNullOrEmpty(invoice.Number))
            {
                // Las Notas Crédito/Débito no tienen rango autorizado propio ante la DIAN, así que
                // no pueden compartir el NextNumber de la resolución de Factura — antes lo hacían,
                // y cada nota consumía un número que le correspondía a la siguiente factura real.
                invoice.Number = invoice.ReferenceDocumentId.HasValue
                    ? (await (documentType?.Code == "ND"
                        ? Fel.Infrastructure.Services.ResolutionNumbering.ClaimNextDebitNoteNumberAsync(_dbContext, client.Id)
                        : Fel.Infrastructure.Services.ResolutionNumbering.ClaimNextCreditNoteNumberAsync(_dbContext, client.Id))).ToString()
                    : (await Fel.Infrastructure.Services.ResolutionNumbering.ClaimNextNumberAsync(_dbContext, resolution.Id)).ToString();
            }

            // El prefijo queda guardado en el documento junto con su consecutivo.
            invoice.Prefix = resolution.Prefix;

            // La DIAN exige que la fecha de emisión coincida con la fecha de firma (regla FAD09e) —
            // no se puede emitir con fecha anterior a hoy. Se refresca acá, en el momento real de
            // envío/firma, en vez de dejar la fecha en que el borrador se creó (que puede ser de
            // días antes si el documento quedó pendiente o se reintenta un envío fallido).
            invoice.IssueDate = Fel.Core.Models.ColombiaTime.Now;

            var municipalities = await _dbContext.DianMunicipalities.AsNoTracking().ToDictionaryAsync(m => m.Code);
            var paymentMeansDianCodes = await _dbContext.TaxCatalogItems.AsNoTracking()
                .Where(t => t.Kind == TaxCatalogKind.PaymentMeans && t.DianCode != null)
                .ToDictionaryAsync(t => t.Category, t => t.DianCode!);

            // ReferenceDocumentId solo tiene valor en notas crédito/débito — antes esto siempre caía
            // en BuildInvoiceData sin importar el tipo real, así que una nota salía literalmente como
            // factura (mismo DianCode="01" y sin el grupo DiscrepancyResponse que exige la DIAN).
            Fel.Core.Models.UblInvoiceData ublData;
            string filePrefix;
            if (invoice.ReferenceDocumentId.HasValue)
            {
                if (documentType == null)
                    throw new InvalidOperationException("Esta nota no tiene un tipo de documento asignado; no se puede determinar su código DIAN.");

                var originalDocument = await _dbContext.Documents.FindAsync(invoice.ReferenceDocumentId.Value)
                    ?? throw new InvalidOperationException("El documento original referenciado por esta nota ya no existe.");

                ublData = documentType.Code == "ND"
                    ? DianDocumentMapper.BuildDebitNoteData(invoice, originalDocument, customer, items, resolution, client, municipalities, documentType, paymentMeansDianCodes)
                    : DianDocumentMapper.BuildCreditNoteData(invoice, originalDocument, customer, items, resolution, client, municipalities, documentType, paymentMeansDianCodes);
                filePrefix = documentType.Code == "ND" ? Fel.Infrastructure.Services.DianFileNaming.NotaDebito : Fel.Infrastructure.Services.DianFileNaming.NotaCredito;
            }
            else
            {
                ublData = DianDocumentMapper.BuildInvoiceData(invoice, customer, items, resolution, client, municipalities, paymentMeansDianCodes, documentType);
                filePrefix = Fel.Infrastructure.Services.DianFileNaming.FacturaVenta;
            }

            var xml = _ublGenerator.GenerateInvoiceXml(ublData);
            var cufe = _ublGenerator.CalculateCufe(ublData);
            invoice.Cufe = cufe;
            invoice.QrCode = _ublGenerator.BuildGraphicQrContent(ublData, cufe);

            string signedXml;
            try
            {
                var certChain = _cryptoVault.GetCertificateChain(certificate.FileName, certificate.EncryptedPassword);
                signedXml = _xmlSigner.SignXml(xml, cert, certChain);
            }
            catch (Exception ex)
            {
                invoice.Status = "REJECTED";
                invoice.DianResponseMessage = $"No se pudo firmar el documento: {ex.Message}";
                invoice.ProcessedAt = DateTime.UtcNow;
                return new DocumentSubmissionResult { Success = false, Status = invoice.Status, Cufe = cufe, ResponseMessage = invoice.DianResponseMessage };
            }

            var fileSequence = await Fel.Infrastructure.Services.DianFileNaming.ClaimNextSequenceAsync(_dbContext, client.Id);
            var entryFileName = Fel.Infrastructure.Services.DianFileNaming.BuildFileName(filePrefix, client.TaxId, fileSequence);

            try
            {
                // SendBillSync (síncrona, un documento a la vez) en vez de SendBillAsync (por lotes)
                // — esta última exige una autorización aparte de la DIAN para envíos masivos que no
                // todo emisor tiene (ver DianStatusPollingWorker/caso SoFactory NIT 900303194); la
                // vía síncrona es la que corresponde a facturación normal y devuelve el veredicto
                // (IsValid/StatusDescription) en la misma respuesta, sin necesitar GetStatusZip
                // después. SendTestSetAsync (por lotes también) sigue aparte, solo para habilitación.
                var dianResponse = await _dianSoapClient.SendDocumentSyncAsync(entryFileName, signedXml, cert, ublData.Environment);
                // Nivel Information silenciado por defecto (ver appsettings.json) — se activa por
                // namespace cuando hace falta ver el veredicto crudo completo (incluye notificaciones
                // no bloqueantes que DianStatusOutcome no expone, ej. FAJ43b/RUT01).
                _logger.LogInformation("[DIAN] Respuesta cruda para {Number}: {DianResponse}", invoice.Number, dianResponse);
                var outcome = DianStatusOutcome.FromGetStatusZipResponse(dianResponse);

                if (!outcome.Resolved)
                {
                    // La DIAN no devolvió veredicto pese al envío síncrono — se deja en PROCESSING
                    // (con el trackId si vino) para que DianStatusPollingWorker lo resuelva después,
                    // en vez de asumir un estado que la DIAN no confirmó.
                    invoice.Status = "PROCESSING";
                    invoice.DianResponseMessage = dianResponse;
                    invoice.DianTrackId = ExtractZipKey(dianResponse);
                    return new DocumentSubmissionResult { Success = true, Status = invoice.Status, Cufe = cufe, ResponseMessage = dianResponse };
                }

                invoice.Status = outcome.Accepted ? "APPROVED" : "REJECTED";
                invoice.DianResponseMessage = outcome.Accepted ? outcome.StatusDescription : outcome.RejectionSummary;
                invoice.ProcessedAt = DateTime.UtcNow;
                return new DocumentSubmissionResult { Success = outcome.Accepted, Status = invoice.Status, Cufe = cufe, ResponseMessage = invoice.DianResponseMessage };
            }
            catch (Exception ex)
            {
                invoice.Status = "REJECTED";
                invoice.DianResponseMessage = ex.Message;
                invoice.ProcessedAt = DateTime.UtcNow;
                return new DocumentSubmissionResult { Success = false, Status = invoice.Status, Cufe = cufe, ResponseMessage = ex.Message };
            }
        }

        // El ZipKey es el identificador que después se usa con GetStatusZip para preguntar el
        // veredicto real (ver DianStatusPollingWorker) — se parsea con XDocument y no con string
        // matching por la misma razón que DianStatusOutcome: la respuesta trae namespaces reales.
        private static string? ExtractZipKey(string soapResponse)
        {
            if (string.IsNullOrWhiteSpace(soapResponse)) return null;
            try
            {
                var doc = System.Xml.Linq.XDocument.Parse(soapResponse);
                return doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "ZipKey")?.Value;
            }
            catch (System.Xml.XmlException)
            {
                return null;
            }
        }
    }
}
