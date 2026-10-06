using System;
using System.Threading.Tasks;
using Fel.Core.Entities;
using Fel.Core.Interfaces;
using Fel.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Fel.Infrastructure.Dian
{
    public class ReceptionEventService : IReceptionEventService
    {
        private readonly FelDbContext _dbContext;
        private readonly ICryptoVault _cryptoVault;
        private readonly IUblGenerator _ublGenerator;
        private readonly IXmlSigner _xmlSigner;
        private readonly IDianSoapClient _dianSoapClient;

        public ReceptionEventService(
            FelDbContext dbContext, ICryptoVault cryptoVault, IUblGenerator ublGenerator,
            IXmlSigner xmlSigner, IDianSoapClient dianSoapClient)
        {
            _dbContext = dbContext;
            _cryptoVault = cryptoVault;
            _ublGenerator = ublGenerator;
            _xmlSigner = xmlSigner;
            _dianSoapClient = dianSoapClient;
        }

        public async Task<ReceivedDocumentEvent> TriggerEventAsync(ReceivedDocument document, Client client, string eventCode)
        {
            var evt = new ReceivedDocumentEvent
            {
                Id = Guid.NewGuid(),
                ReceivedDocumentId = document.Id,
                EventCode = eventCode,
                SentAt = DateTime.UtcNow
            };

            var certificate = await _dbContext.Certificates.FirstOrDefaultAsync(c => c.ClientId == client.Id && c.IsActive);
            if (certificate == null)
            {
                evt.Status = "REJECTED";
                evt.DianResponseMessage = "Este emisor no tiene un certificado digital activo cargado.";
                _dbContext.ReceivedDocumentEvents.Add(evt);
                await _dbContext.SaveChangesAsync();
                return evt;
            }

            var cert = _cryptoVault.GetCertificate(certificate.FileName, certificate.EncryptedPassword);
            var certificateNit = _cryptoVault.ExtractNit(cert);
            if (!NitValidation.Matches(certificateNit, client.TaxId))
            {
                evt.Status = "REJECTED";
                evt.DianResponseMessage = $"El NIT del certificado ({certificateNit ?? "no encontrado"}) no coincide con el NIT registrado del emisor ({client.TaxId}).";
                _dbContext.ReceivedDocumentEvents.Add(evt);
                await _dbContext.SaveChangesAsync();
                return evt;
            }

            var ublData = ReceptionEventMapper.BuildEventData(document, client, eventCode);
            var cude = _ublGenerator.CalculateEventCude(ublData);
            var xml = _ublGenerator.GenerateEventXml(ublData, cude);
            evt.Cude = cude;

            string signedXml;
            try
            {
                var certChain = _cryptoVault.GetCertificateChain(certificate.FileName, certificate.EncryptedPassword);
                signedXml = _xmlSigner.SignXml(xml, cert, certChain);
            }
            catch (Exception ex)
            {
                evt.Status = "REJECTED";
                evt.DianResponseMessage = $"No se pudo firmar el evento: {ex.Message}";
                _dbContext.ReceivedDocumentEvents.Add(evt);
                await _dbContext.SaveChangesAsync();
                return evt;
            }

            try
            {
                var dianResponse = await _dianSoapClient.SendEventUpdateStatusAsync($"AR{ublData.DocumentNumber}.xml", signedXml, cert, ublData.Environment);
                evt.Status = "SENT";
                evt.DianResponseMessage = dianResponse;
            }
            catch (Exception ex)
            {
                evt.Status = "REJECTED";
                evt.DianResponseMessage = $"La DIAN rechazó el evento: {ex.Message}";
            }

            _dbContext.ReceivedDocumentEvents.Add(evt);
            await _dbContext.SaveChangesAsync();
            return evt;
        }

        public async Task TriggerEnabledEventsAsync(ReceivedDocument document, Client client)
        {
            // Los eventos automáticos son los de la sucursal que recibió el documento (o, si no tiene propios, los de la principal).
            var events = await new Fel.Infrastructure.Services.BranchCredentialResolver(_dbContext).ReceptionEventsAsync(client.Id, document.BranchId);
            if (events.AcuseRecibo) await TriggerEventAsync(document, client, "030");
            if (events.ReciboBien) await TriggerEventAsync(document, client, "032");
            if (events.Aceptacion) await TriggerEventAsync(document, client, "033");
            if (events.Reclamo) await TriggerEventAsync(document, client, "031");
        }
    }
}
