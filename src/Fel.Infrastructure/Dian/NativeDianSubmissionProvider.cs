using System;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Fel.Core.Entities;
using Fel.Core.Interfaces;
using Fel.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

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

        public string IntegratorCode => "NATIVE";

        public NativeDianSubmissionProvider(
            FelDbContext dbContext, IUblGenerator ublGenerator, IXmlSigner xmlSigner,
            ICryptoVault cryptoVault, IDianSoapClient dianSoapClient)
        {
            _dbContext = dbContext;
            _ublGenerator = ublGenerator;
            _xmlSigner = xmlSigner;
            _cryptoVault = cryptoVault;
            _dianSoapClient = dianSoapClient;
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

            if (string.IsNullOrEmpty(invoice.Number))
            {
                invoice.Number = (await Fel.Infrastructure.Services.ResolutionNumbering.ClaimNextNumberAsync(_dbContext, resolution.Id)).ToString();
            }

            var municipalities = await _dbContext.DianMunicipalities.AsNoTracking().ToDictionaryAsync(m => m.Code);
            var ublData = DianDocumentMapper.BuildInvoiceData(invoice, customer, items, resolution, client, municipalities);

            var xml = _ublGenerator.GenerateInvoiceXml(ublData);
            var cufe = _ublGenerator.CalculateCufe(ublData);
            invoice.Cufe = cufe;

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
            var zipFileName = Fel.Infrastructure.Services.DianFileNaming.BuildFileName("z", client.TaxId, fileSequence);
            var entryFileName = Fel.Infrastructure.Services.DianFileNaming.BuildFileName(Fel.Infrastructure.Services.DianFileNaming.FacturaVenta, client.TaxId, fileSequence);

            try
            {
                var dianResponse = await _dianSoapClient.SendBillAsync(zipFileName, entryFileName, signedXml, cert, ublData.Environment);

                invoice.Status = "PROCESSING";
                invoice.DianResponseMessage = dianResponse;
                return new DocumentSubmissionResult { Success = true, Status = invoice.Status, Cufe = cufe, ResponseMessage = dianResponse };
            }
            catch (Exception ex)
            {
                invoice.Status = "REJECTED";
                invoice.DianResponseMessage = ex.Message;
                invoice.ProcessedAt = DateTime.UtcNow;
                return new DocumentSubmissionResult { Success = false, Status = invoice.Status, Cufe = cufe, ResponseMessage = ex.Message };
            }
        }
    }
}
