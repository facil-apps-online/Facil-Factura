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
            // Un mismo documento/tipo de evento solo puede tener una operación activa o confirmada.
            // El lock se mantiene durante la llamada externa para cubrir API, worker y varias instancias.
            await using var eventLock = await Fel.Infrastructure.Services.SqlAppLock.AcquireAsync(
                _dbContext, $"ReceptionEvent:{client.Id}:{document.Id}:{eventCode}", timeoutMilliseconds: 240000);
            if (eventLock == null)
            {
                return new ReceivedDocumentEvent
                {
                    Id = Guid.NewGuid(),
                    ReceivedDocumentId = document.Id,
                    EventCode = eventCode,
                    Status = "PENDING",
                    SentAt = DateTime.UtcNow,
                    DianResponseMessage = "El evento ya está siendo procesado por otra operación."
                };
            }

            // Un evento ya aceptado no se vuelve a enviar, aunque después haya intentos rechazados o
            // duplicados: se devuelve el aceptado, sin importar con qué consecutivo salió.
            var confirmed = await _dbContext.ReceivedDocumentEvents
                .Where(e => e.ReceivedDocumentId == document.Id && e.EventCode == eventCode && e.Status == "SENT")
                .OrderByDescending(e => e.SentAt)
                .FirstOrDefaultAsync();
            if (confirmed != null)
                return confirmed;

            // Los rechazos explícitos sí pueden reintentarse. Un DUPLICATE no: Dataico ya tiene un
            // evento de ese tipo para esta compra (registrado por fuera o sin respuesta local), y
            // reenviarlo solo produce otro duplicado.
            var existing = await _dbContext.ReceivedDocumentEvents
                .Where(e => e.ReceivedDocumentId == document.Id && e.EventCode == eventCode)
                .OrderByDescending(e => e.SentAt)
                .FirstOrDefaultAsync();
            if (existing?.Status == "DUPLICATE")
                return existing;

            // Un evento fuera de orden lo rechaza la DIAN, pero Dataico lo deja registrado como el evento de esa compra y ya no deja
            // reenviarlo. Por eso se valida antes de llamar al proveedor; no se guarda ni consume consecutivo.
            var missing = await MissingPrerequisiteAsync(document.Id, eventCode);
            if (missing != null)
            {
                return new ReceivedDocumentEvent
                {
                    Id = Guid.NewGuid(),
                    ReceivedDocumentId = document.Id,
                    EventCode = eventCode,
                    Status = "REJECTED",
                    SentAt = DateTime.UtcNow,
                    DianResponseMessage = missing
                };
            }

            // Con el lock tomado nadie más está enviando este evento: un PENDING es una operación que se
            // cayó antes de recibir respuesta (el consecutivo no alcanzó a guardarse). Se cierra y se
            // intenta de nuevo; si Dataico ya lo había registrado, responderá que el evento ya existe.
            if (existing?.Status == "PENDING")
            {
                existing.Status = "REJECTED";
                existing.DianResponseMessage = "Operación interrumpida sin respuesta de Dataico; se reintentó el envío.";
                await _dbContext.SaveChangesAsync();
            }

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
                var result = await TriggerEventAsync(document, client, code.Item2);
                // Como el legacy: si un evento no queda registrado, los siguientes no se envían (dependen de él).
                if (result.Status is not ("SENT" or "DUPLICATE")) break;
                // Dataico rechaza peticiones paralelas o muy seguidas ("Demasiadas peticiones paralelas"): una pausa entre eventos, como el legacy.
                if (viaDataico) await Task.Delay(1200);
            }
        }

        private static string EventName(string eventCode) => eventCode switch
        {
            "030" => "acuse de recibo (030)",
            "031" => "reclamo (031)",
            "032" => "recibo del bien o servicio (032)",
            "033" => "aceptación expresa (033)",
            _ => eventCode
        };

        // Orden que exige la DIAN: el recibo del bien (032) después del acuse (030); la aceptación expresa (033) y el reclamo (031)
        // después de ambos, y son excluyentes entre sí. Un DUPLICATE cuenta como hecho: Dataico ya tiene ese evento para la compra.
        private async Task<string?> MissingPrerequisiteAsync(Guid documentId, string eventCode)
        {
            var done = await _dbContext.ReceivedDocumentEvents
                .Where(e => e.ReceivedDocumentId == documentId && (e.Status == "SENT" || e.Status == "DUPLICATE"))
                .Select(e => e.EventCode)
                .Distinct()
                .ToListAsync();

            string[] required = eventCode switch
            {
                "032" => new[] { "030" },
                "031" or "033" => new[] { "030", "032" },
                _ => Array.Empty<string>()
            };
            var pending = required.FirstOrDefault(code => !done.Contains(code));
            if (pending != null)
                return $"Primero debe quedar registrado el {EventName(pending)}. No se envió el {EventName(eventCode)} ni se usó consecutivo.";

            var exclusive = eventCode switch { "031" => "033", "033" => "031", _ => null };
            if (exclusive != null && done.Contains(exclusive))
                return $"La factura ya tiene {EventName(exclusive)}; no admite también {EventName(eventCode)}.";

            return null;
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

        // El evento ya existe en Dataico para esta compra y tipo: está hecho, aunque no tengamos su comprobante.
        private static bool IsEventAlreadyRegistered(string body) =>
            body.Contains("Ya tienes evento de recepción con esa compra y tipo de evento");

        // El consecutivo ya lo usa otro evento aceptado ("El documento 'N' ya esta aceptado en la Dian", code 002):
        // el evento de esta compra NO quedó registrado. No es un duplicado; se reintenta con otro consecutivo.
        private static bool IsEventNumberTaken(string body) =>
            body.Contains("ya esta aceptado en la Dian")
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
                // El semaforo estatico solo cubre una instancia del proceso. Este
                // lock de SQL tambien coordina la API y el worker.
                await using var distributedLock = await Fel.Infrastructure.Services.SqlAppLock.AcquireAsync(
                    _dbContext, $"ReceptionEventNumbering:{client.Id}", timeoutMilliseconds: 240000);
                if (distributedLock == null)
                {
                    evt.Status = "REJECTED";
                    evt.DianResponseMessage = "No se pudo reservar el consecutivo RADIAN porque otro envio sigue en curso.";
                    _dbContext.ReceivedDocumentEvents.Add(evt);
                    await _dbContext.SaveChangesAsync();
                    return evt;
                }
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

            // Igual que en facturación, no se envía a ciegas. Dataico es la fuente
            // de verdad: si el consecutivo ya existe, se busca el siguiente libre;
            // si la consulta falla por red/proveedor, se detiene el envío.
            var numberAvailable = false;
            for (var availabilityAttempt = 0; availabilityAttempt < 100; availabilityAttempt++)
            {
                var availability = await _dataicoApiService.GetReceptionEventAsync($"{eventPrefix}{number}", credentials);
                if (availability.Success)
                {
                    number++;
                    continue;
                }

                if (availability.StatusCode != 404)
                    return await Finish("REJECTED", $"No se pudo validar la disponibilidad del consecutivo {eventPrefix}{number} en Dataico ({availability.StatusCode}). No se realizó el envío.");

                numberAvailable = true;
                break;
            }
            if (!numberAvailable)
                return await Finish("REJECTED", $"No se encontró un consecutivo RADIAN libre en Dataico a partir de {eventPrefix}{number}.");

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
                // AdvancePast y no "si es el actual": si se saltaron números ocupados en Dataico, el control local también los salta.
                await Fel.Infrastructure.Services.ReceptionEventNumberingService.AdvancePastAsync(_dbContext, client.Id, number);
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

            // En ninguno de los dos casos Dataico usó el consecutivo de este intento: no se le asigna al evento
            // ni se avanza la numeración, para no dejar un hueco local que Dataico después obligue a reutilizar.
            // Tampoco se consulta el evento por ese consecutivo: o no existe, o es de otra compra.
            if (IsEventAlreadyRegistered(result.RawResponse))
            {
                evt.ProviderResponse = result.RawResponse;
                return await Finish("DUPLICATE", "Dataico ya tiene un evento de este tipo para la compra y no deja reenviarlo. No hay comprobante local (CUDE): verifica en Dataico si la DIAN lo aceptó.");
            }

            var detail = string.IsNullOrWhiteSpace(result.ErrorMessage) ? result.RawResponse : result.ErrorMessage;
            evt.ProviderResponse = result.RawResponse;

            // Un rechazo de la DIAN también deja el número ocupado en Dataico (queda como DIAN_RECHAZADO), igual que el choque de
            // consecutivo: se comprueba y, si existe, la numeración local lo salta para no volver a probarlo en el siguiente envío.
            var occupied = await _dataicoApiService.GetReceptionEventAsync($"{eventPrefix}{number}", credentials);
            if (occupied.Success)
                await Fel.Infrastructure.Services.ReceptionEventNumberingService.AdvancePastAsync(_dbContext, client.Id, number);

            if (IsEventNumberTaken(result.RawResponse))
                return await Finish("REJECTED", $"El consecutivo {eventPrefix}{number} ya lo usa otro evento en Dataico; este evento no quedó registrado. Puedes reintentarlo. Respuesta de Dataico: {detail}");

            return await Finish("REJECTED", $"Dataico rechazó el evento ({result.StatusCode}): {detail}");
        }
    }
}
