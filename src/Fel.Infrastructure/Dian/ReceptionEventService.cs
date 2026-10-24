using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using Fel.Core.Entities;
using Fel.Core.Interfaces;
using Fel.Infrastructure.Data;
using Fel.Infrastructure.Dataico;
using Fel.Infrastructure.Dataico.Models;
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
        private readonly IDataicoApiService _dataicoApiService;
        private readonly ICryptoService _cryptoService;
        private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> NumberingLocks = new();

        public ReceptionEventService(
            FelDbContext dbContext, ICryptoVault cryptoVault, IUblGenerator ublGenerator,
            IXmlSigner xmlSigner, IDianSoapClient dianSoapClient,
            IDataicoApiService dataicoApiService, ICryptoService cryptoService)
        {
            _dbContext = dbContext;
            _cryptoVault = cryptoVault;
            _ublGenerator = ublGenerator;
            _xmlSigner = xmlSigner;
            _dianSoapClient = dianSoapClient;
            _dataicoApiService = dataicoApiService;
            _cryptoService = cryptoService;
        }

        // Con Dataico quien firma y envía el evento a la DIAN es Dataico (no hace falta certificado propio);
        // con emisión directa lo firmamos nosotros con el certificado del Client.
        private async Task<bool> UsesDataicoAsync(Client client) =>
            await _dbContext.Clients.AsNoTracking().Where(c => c.Id == client.Id).Select(c => c.Integrator.Code).FirstOrDefaultAsync() == "DATAICO";

        public async Task<ReceivedDocumentEvent> TriggerEventAsync(ReceivedDocument document, Client client, string eventCode)
        {
            var evt = new ReceivedDocumentEvent
            {
                Id = Guid.NewGuid(),
                ReceivedDocumentId = document.Id,
                EventCode = eventCode,
                SentAt = DateTime.UtcNow
            };

            // También validamos documentos antiguos que fueron guardados antes de distinguir
            // InvoiceTypeCode=05; así nunca se intenta enviar un Documento Soporte a RADIAN.
            try
            {
                ReceivedDocumentParser.Parse(document.RawXml);
            }
            catch (NotSupportedException ex)
            {
                evt.Status = "REJECTED";
                evt.DianResponseMessage = ex.Message;
                _dbContext.ReceivedDocumentEvents.Add(evt);
                await _dbContext.SaveChangesAsync();
                return evt;
            }

            if (await UsesDataicoAsync(client))
                return await TriggerViaDataicoAsync(document, client, evt);

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
            if (!document.IsCredit) return;
            // Los eventos automáticos son los de la sucursal que recibió el documento (o, si no tiene propios, los de la principal).
            var events = await new Fel.Infrastructure.Services.BranchCredentialResolver(_dbContext).ReceptionEventsAsync(client.Id, document.BranchId);
            var viaDataico = await UsesDataicoAsync(client);

            foreach (var code in new[] { (events.AcuseRecibo, "030"), (events.ReciboBien, "032"), (events.Aceptacion, "033"), (events.Reclamo, "031") })
            {
                if (!code.Item1) continue;
                await TriggerEventAsync(document, client, code.Item2);
                // Dataico rechaza peticiones paralelas o muy seguidas ("Demasiadas peticiones paralelas"): una pausa entre eventos, como el legacy.
                if (viaDataico) await Task.Delay(1200);
            }
        }

        // Nombre que Dataico usa para cada código de evento.
        private static string DataicoEventType(string eventCode) => eventCode switch
        {
            "030" => "ACUSE_DE_RECIBO",
            "031" => "RECLAMO",
            "032" => "RECIBO_DE_PRESTACION",
            "033" => "ACEPTACION_EXPRESA",
            _ => throw new NotSupportedException($"Código de evento no soportado: '{eventCode}'.")
        };

        // El evento ya existe en Dataico para ese documento y tipo: cuenta como enviado.
        private static bool IsDuplicateEvent(string body) =>
            body.Contains("Ya tienes evento de recepción con esa compra y tipo de evento")
            || body.Contains("ya esta aceptado en la Dian")
            || body.Contains("\"code\":\"002\"");

        // Límite de peticiones o demora de la pasarela de la DIAN: se reintenta.
        private static bool IsRetryable(DataicoResult result) =>
            result.StatusCode is 429 or 504
            || result.RawResponse.Contains("Demasiadas peticiones paralelas")
            || result.RawResponse.Contains("Gateway Time-out");

        private static int? SuggestedEventNumber(DataicoResult result)
        {
            var match = Regex.Match(result.RawResponse ?? string.Empty, @"Vamos a usar:\s*'(?<number>\d+)'", RegexOptions.IgnoreCase);
            return match.Success && int.TryParse(match.Groups["number"].Value, out var number) ? number : null;
        }

        private static string DataicoDocumentType(string documentTypeCode) => documentTypeCode switch
        {
            "91" => "NOTA_CREDITO",
            "92" => "NOTA_DEBITO",
            _ => "FACTURA_VENTA"
        };

        // Dataico espera el código DANE municipal sin el prefijo del departamento (Bogotá: 001, no 11001).
        private static string NormalizeEventCity(string? city, string? department)
        {
            var cityDigits = new string((city ?? string.Empty).Where(char.IsDigit).ToArray());
            var departmentDigits = new string((department ?? string.Empty).Where(char.IsDigit).ToArray());

            if (cityDigits.Length == 5 && departmentDigits.Length == 2 && cityDigits.StartsWith(departmentDigits, StringComparison.Ordinal))
                return cityDigits[2..];

            return cityDigits.Length > 3 ? cityDigits[^3..] : cityDigits;
        }

        private static string NormalizeEventDepartment(string? department)
        {
            var departmentDigits = new string((department ?? string.Empty).Where(char.IsDigit).ToArray());
            return departmentDigits.Length >= 2 ? departmentDigits[^2..] : departmentDigits;
        }

        private async Task<ReceivedDocumentEvent> TriggerViaDataicoAsync(ReceivedDocument document, Client client, ReceivedDocumentEvent evt)
        {
            var numberingLock = NumberingLocks.GetOrAdd(client.Id, _ => new SemaphoreSlim(1, 1));
            await numberingLock.WaitAsync();
            try
            {
                return await TriggerViaDataicoCoreAsync(document, client, evt);
            }
            finally
            {
                numberingLock.Release();
            }
        }

        private async Task<ReceivedDocumentEvent> TriggerViaDataicoCoreAsync(ReceivedDocument document, Client client, ReceivedDocumentEvent evt)
        {
            async Task<ReceivedDocumentEvent> Finish(string status, string? message)
            {
                evt.Status = status;
                evt.DianResponseMessage = message;
                if (_dbContext.Entry(evt).State == EntityState.Detached)
                    _dbContext.ReceivedDocumentEvents.Add(evt);
                await _dbContext.SaveChangesAsync();
                return evt;
            }

            var credentials = DataicoDocumentMapper.ToCredentials(client, _cryptoService);
            if (string.IsNullOrEmpty(credentials.AuthToken))
                return await Finish("REJECTED", "Este emisor no tiene configuradas las credenciales de Dataico.");

            // Datos del proveedor tomados del XML del documento; si no se pueden leer, al menos NIT y nombre ya guardados.
            ParsedIssuer issuer;
            try { issuer = ReceivedDocumentParser.ParseIssuer(document.RawXml); }
            catch { issuer = new ParsedIssuer(); }
            if (string.IsNullOrWhiteSpace(issuer.Identification)) issuer.Identification = document.IssuerTaxId;
            if (string.IsNullOrWhiteSpace(issuer.CompanyName)) issuer.CompanyName = document.IssuerName;

            // Quien realiza el evento es el Client; su correo es el del buzón donde llegó el documento.
            var mailbox = await new Fel.Infrastructure.Services.BranchCredentialResolver(_dbContext).ReceptionMailboxAsync(client.Id, document.BranchId);
            var personEmail = mailbox.User.Contains('@') ? mailbox.User : client.Email;

            // Reservamos el consecutivo antes de llamar a Dataico. La transacción serializable evita que dos
            // cargas simultáneas calculen el mismo número. Así también queda guardado si Dataico responde
            // que el evento ya existía y no se vuelve a reutilizar en el siguiente intento.
            var receptionNumbering = await Fel.Infrastructure.Services.ReceptionEventNumberingService.GetOrCreateAsync(_dbContext, client.Id);

            int number = checked((int)await Fel.Infrastructure.Services.ReceptionEventNumberingService.PeekNextAsync(_dbContext, client.Id));
            /*
                // Compatibilidad temporal con clientes creados antes de mover esta numeración a Resoluciones.
                var lastNumber = await _dbContext.ReceivedDocumentEvents
                    .Where(e => e.ReceivedDocument.ClientId == client.Id && e.EventNumber != null)
                    .MaxAsync(e => e.EventNumber) ?? 0;
                var legacyUnnumbered = await _dbContext.ReceivedDocumentEvents
                    .Where(e => e.ReceivedDocument.ClientId == client.Id && e.EventNumber == null)
                    .CountAsync();
                number = Math.Max(client.ReceptionEventStartNumber, lastNumber + legacyUnnumbered + 1);
            */

            evt.Status = "PENDING";
            _dbContext.ReceivedDocumentEvents.Add(evt);
            await _dbContext.SaveChangesAsync();

            var eventPrefix = receptionNumbering.Prefix;

            var request = new DataicoReceptionEventEnvelope
            {
                reception_event = new DataicoReceptionEvent
                {
                    env = credentials.Environment,
                    event_type = DataicoEventType(evt.EventCode),
                    document_type = DataicoDocumentType(document.DocumentTypeCode),
                    document_number = document.DocumentId,
                    document_cufe = document.Cufe,
                    issue_date = Fel.Core.Models.ColombiaTime.Now.ToString("dd/MM/yyyy"),
                    person = new DataicoEventPerson
                    {
                        first_name = client.CompanyName,
                        party_identification = client.TaxId,
                        email = personEmail
                    },
                    number = number.ToString(),
                    receiver = new DataicoEventReceiver
                    {
                        party_identification_type = issuer.IdentificationType,
                        party_identification = issuer.Identification,
                        party_type = issuer.PartyType,
                        tax_level_code = issuer.TaxLevelCode,
                        regimen = issuer.Regimen,
                        company_name = issuer.CompanyName,
                        first_name = issuer.FirstName,
                        family_name = issuer.FamilyName,
                        address_line = issuer.Address,
                        email = issuer.Email,
                        phone = issuer.Phone,
                        city = NormalizeEventCity(issuer.City, issuer.Department),
                        department = NormalizeEventDepartment(issuer.Department)
                    }
                }
            };

            const int maxRetries = 3;
            var delay = TimeSpan.FromSeconds(4);
            DataicoResult result;
            for (var attempt = 0; ; attempt++)
            {
                result = await _dataicoApiService.SendReceptionEventAsync(request, credentials);
                if (result.Success || !IsRetryable(result) || attempt >= maxRetries) break;
                await Task.Delay(delay);
                delay *= 2;
            }

            // Dataico puede detectar que la cuenta tiene números pendientes anteriores y devolver
            // explícitamente el siguiente válido. Alinear el control local y repetir una sola vez
            // evita dejar el evento rechazado con un consecutivo que Dataico nunca aceptará.
            var suggestedNumber = SuggestedEventNumber(result);
            if (!result.Success && suggestedNumber.HasValue && suggestedNumber.Value != number)
            {
                number = suggestedNumber.Value;
                request.reception_event.number = number.ToString();
                await Fel.Infrastructure.Services.ReceptionEventNumberingService.MoveBackIfAheadAsync(_dbContext, client.Id, number);
                await _dbContext.SaveChangesAsync();
                result = await _dataicoApiService.SendReceptionEventAsync(request, credentials);
            }

            if (result.Success)
            {
                evt.EventNumber = number;
                await Fel.Infrastructure.Services.ReceptionEventNumberingService.AdvanceIfCurrentAsync(_dbContext, client.Id, number);
                evt.ProviderUid = result.DataicoDocumentId;
                evt.Cude = result.Cufe ?? string.Empty;
                evt.ProviderResponse = result.RawResponse;
                evt.ProviderXmlUrl = result.XmlUrl;
                evt.ProviderPdfUrl = result.PdfUrl;

                // Dataico aceptó pero a veces no devuelve el CUDE en la misma respuesta (demora interna): se consulta por su número.
                if (string.IsNullOrEmpty(evt.Cude))
                {
                    await Task.Delay(TimeSpan.FromSeconds(3));
                    var lookup = await _dataicoApiService.GetReceptionEventAsync(eventPrefix + number, credentials);
                    if (lookup.Success)
                    {
                        evt.Cude = lookup.Cufe ?? string.Empty;
                        evt.ProviderUid ??= lookup.DataicoDocumentId;
                        evt.ProviderResponse ??= lookup.RawResponse;
                        evt.ProviderXmlUrl ??= lookup.XmlUrl;
                        evt.ProviderPdfUrl ??= lookup.PdfUrl;
                    }
                }

                return await Finish("SENT", string.IsNullOrEmpty(evt.Cude)
                    ? "Dataico recibió el evento pero todavía no entregó su CUDE."
                    : "Evento enviado a la DIAN por Dataico.");
            }

            if (IsDuplicateEvent(result.RawResponse))
            {
                evt.EventNumber = number;
                await Fel.Infrastructure.Services.ReceptionEventNumberingService.AdvanceIfCurrentAsync(_dbContext, client.Id, number);
                evt.ProviderUid = result.DataicoDocumentId;
                evt.Cude = result.Cufe ?? string.Empty;
                evt.ProviderResponse = result.RawResponse;
                evt.ProviderXmlUrl = result.XmlUrl;
                evt.ProviderPdfUrl = result.PdfUrl;
                var lookup = await _dataicoApiService.GetReceptionEventAsync(eventPrefix + number, credentials);
                if (lookup.Success)
                {
                    evt.ProviderUid ??= lookup.DataicoDocumentId;
                    evt.Cude = string.IsNullOrWhiteSpace(evt.Cude) ? lookup.Cufe ?? string.Empty : evt.Cude;
                    evt.ProviderResponse ??= lookup.RawResponse;
                    evt.ProviderXmlUrl ??= lookup.XmlUrl;
                    evt.ProviderPdfUrl ??= lookup.PdfUrl;
                }
                return await Finish("DUPLICATE", $"Dataico informa que este evento ya existe para el documento. Consecutivo consultado: {eventPrefix}{number}.");
            }

            var detail = string.IsNullOrWhiteSpace(result.ErrorMessage) ? result.RawResponse : result.ErrorMessage;
            return await Finish("REJECTED", $"Dataico rechazó el evento ({result.StatusCode}): {detail}");
        }
    }
}
