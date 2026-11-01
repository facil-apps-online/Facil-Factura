using System.Security.Cryptography.X509Certificates;
using Fel.Core.Entities;
using Fel.Core.Interfaces;
using Fel.Infrastructure.Data;
using Fel.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Fel.Infrastructure.Dian
{
    // Deja listo el contenedor AttachedDocument de un documento aprobado: guarda la respuesta de la DIAN, arma el contenedor con la factura
    // firmada que ya se guardó y lo firma con el certificado del emisor. Se llama al aprobarse el documento; si en ese momento falla, el
    // reenvío vuelve a llamarlo y, si hace falta, le pide a la DIAN su respuesta (GetStatus por CUFE).
    public class AttachedDocumentService
    {
        private readonly FelDbContext _db;
        private readonly ICryptoVault _cryptoVault;
        private readonly IXmlSigner _xmlSigner;
        private readonly IDianSoapClient _dianSoapClient;
        private readonly ILogger<AttachedDocumentService> _logger;

        public AttachedDocumentService(FelDbContext db, ICryptoVault cryptoVault, IXmlSigner xmlSigner, IDianSoapClient dianSoapClient, ILogger<AttachedDocumentService> logger)
        {
            _db = db;
            _cryptoVault = cryptoVault;
            _xmlSigner = xmlSigner;
            _dianSoapClient = dianSoapClient;
            _logger = logger;
        }

        // Devuelve el contenedor firmado, o el motivo por el que no se pudo tener.
        public async Task<(string? Xml, string? Error)> EnsureAsync(Document document, Client client, string? applicationResponseXml = null, CancellationToken ct = default)
        {
            try
            {
                var stored = await DocumentSignedXmlStore.LoadAsync(_db, document.Id, ct);
                if (stored == null) return (null, "El documento no tiene guardado su XML firmado.");
                if (stored.AttachedDocument != null) return (stored.AttachedDocument, null);

                var certificate = await _db.Certificates.FirstOrDefaultAsync(c => c.ClientId == client.Id && c.IsActive, ct);
                if (certificate == null) return (null, "El emisor no tiene un certificado digital activo para firmar el contenedor.");
                var cert = _cryptoVault.GetCertificate(certificate.FileName, certificate.EncryptedPassword);
                var chain = _cryptoVault.GetCertificateChain(certificate.FileName, certificate.EncryptedPassword);

                var applicationResponse = applicationResponseXml ?? stored.ApplicationResponse;
                if (applicationResponse == null)
                {
                    applicationResponse = await FetchApplicationResponseAsync(document, client, cert);
                    if (applicationResponse == null) return (null, "La DIAN no entregó su respuesta de aprobación de este documento.");
                }
                if (stored.ApplicationResponse == null)
                    await DocumentSignedXmlStore.SaveApplicationResponseAsync(_db, document.Id, applicationResponse, ct);

                var containerId = stored.FileSequence > 0 ? stored.FileSequence : Math.Max(1, client.DianFileSequence);
                var unsigned = AttachedDocumentBuilder.Build(stored.SignedXml, applicationResponse, containerId, Fel.Core.Models.ColombiaTime.Now);
                var signed = _xmlSigner.SignXml(unsigned, cert, chain);
                await DocumentSignedXmlStore.SaveAttachedDocumentAsync(_db, document.Id, signed, ct);
                return (signed, null);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "No se pudo armar el contenedor AttachedDocument del documento {DocumentId}.", document.Id);
                return (null, $"No se pudo armar el contenedor del documento: {ex.Message}");
            }
        }

        private async Task<string?> FetchApplicationResponseAsync(Document document, Client client, X509Certificate2 cert)
        {
            if (string.IsNullOrWhiteSpace(document.Cufe)) return null;
            var environment = client.DianHabilitationStatus == "Production" ? "1" : "2";
            var response = await _dianSoapClient.GetStatusAsync(document.Cufe, cert, environment);
            var outcome = DianStatusOutcome.FromGetStatusZipResponse(response);
            return outcome is { Resolved: true, Accepted: true } ? outcome.ApplicationResponseXml : null;
        }
    }
}
