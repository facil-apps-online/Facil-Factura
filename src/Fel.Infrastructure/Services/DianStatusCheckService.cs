using System;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Fel.Core.Entities;
using Fel.Core.Interfaces;
using Fel.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Fel.Infrastructure.Services
{
    // Le pregunta a la DIAN el veredicto real (GetStatusZip) de un documento que quedó en
    // "PROCESSING" tras NativeDianSubmissionProvider.SubmitAsync — ese envío solo confirma que el
    // paquete llegó, no si la DIAN lo aceptó o lo rechazó. Compartido entre el worker periódico
    // (DianStatusPollingWorker) y cualquier consulta puntual que se necesite más adelante.
    public class DianStatusCheckService
    {
        private readonly FelDbContext _dbContext;
        private readonly ICryptoVault _cryptoVault;
        private readonly IDianSoapClient _dianSoapClient;
        private readonly ILogger<DianStatusCheckService> _logger;

        public DianStatusCheckService(FelDbContext dbContext, ICryptoVault cryptoVault, IDianSoapClient dianSoapClient, ILogger<DianStatusCheckService> logger)
        {
            _dbContext = dbContext;
            _cryptoVault = cryptoVault;
            _dianSoapClient = dianSoapClient;
            _logger = logger;
        }

        // true si el documento quedó resuelto (APPROVED/REJECTED) — false si la DIAN todavía no
        // termina de validar o si hubo un error consultando (en ambos casos se reintenta después).
        public async Task<bool> CheckAndUpdateAsync(Document document, Client client)
        {
            if (string.IsNullOrEmpty(document.DianTrackId))
            {
                _logger.LogWarning("Documento {DocumentId} está en PROCESSING sin DianTrackId — no se puede consultar su estado.", document.Id);
                return false;
            }

            var certificate = await _dbContext.Certificates.FirstOrDefaultAsync(c => c.ClientId == client.Id && c.IsActive);
            if (certificate == null)
            {
                _logger.LogWarning("Documento {DocumentId}: el emisor {ClientId} ya no tiene un certificado activo para consultar el estado.", document.Id, client.Id);
                return false;
            }

            X509Certificate2 cert;
            try
            {
                cert = _cryptoVault.GetCertificate(certificate.FileName, certificate.EncryptedPassword);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Documento {DocumentId}: no se pudo cargar el certificado para consultar el estado.", document.Id);
                return false;
            }

            var environment = client.DianHabilitationStatus == "Production" ? "1" : "2";

            string soapResponse;
            try
            {
                soapResponse = await _dianSoapClient.GetStatusZipAsync(document.DianTrackId, cert, environment);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Documento {DocumentId}: error consultando GetStatusZip con trackId {TrackId}.", document.Id, document.DianTrackId);
                return false;
            }

            var outcome = DianStatusOutcome.FromGetStatusZipResponse(soapResponse);
            if (!outcome.Resolved)
            {
                return false;
            }

            document.Status = outcome.Accepted ? "APPROVED" : "REJECTED";
            document.DianResponseMessage = outcome.Accepted ? outcome.StatusDescription : outcome.RejectionSummary;
            document.ProcessedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Documento {DocumentId} resuelto por la DIAN: {Status}.", document.Id, document.Status);
            return true;
        }
    }
}
