using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Fel.Core.Entities;
using Fel.Core.Interfaces;
using Fel.Core.Models;
using Fel.Infrastructure.Data;
using Fel.Infrastructure.Dian;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Fel.Infrastructure.Services
{
    // Envía documentos reales al Set de Pruebas de habilitación de la DIAN (operación SOAP
    // SendTestSetAsync, distinta de SendBillAsync), usando la resolución de prueba (DocumentType
    // "FE-TEST") que DianHabilitationScraperService ya leyó de la DIAN — nunca genera XML de
    // producción ni usa la resolución real del Client. Reemplaza al runner simulado anterior
    // (DianTestSetRunnerService), que nunca llegó a hablar con la DIAN de verdad.
    //
    // A diferencia del flujo de facturación normal, acá no se crean/persisten Document/Customer:
    // los datos de prueba se arman directamente como InvoiceRequest/CreditNoteRequest/
    // DebitNoteRequest (mismo modelo que usa la API de integración B2B) porque el contenido no
    // importa para la DIAN, solo que el XML sea válido.
    public class DianTestSetSubmissionService
    {
        private readonly FelDbContext _dbContext;
        private readonly IUblGenerator _ublGenerator;
        private readonly IXmlSigner _xmlSigner;
        private readonly ICryptoVault _cryptoVault;
        private readonly IDianSoapClient _dianSoapClient;
        private readonly ILogger<DianTestSetSubmissionService> _logger;

        public DianTestSetSubmissionService(
            FelDbContext dbContext, IUblGenerator ublGenerator, IXmlSigner xmlSigner,
            ICryptoVault cryptoVault, IDianSoapClient dianSoapClient, ILogger<DianTestSetSubmissionService> logger)
        {
            _dbContext = dbContext;
            _ublGenerator = ublGenerator;
            _xmlSigner = xmlSigner;
            _cryptoVault = cryptoVault;
            _dianSoapClient = dianSoapClient;
            _logger = logger;
        }

        // Envía UN documento de prueba y devuelve el resultado crudo de la DIAN — se llama una vez
        // por documento en vez de disparar los N de una sola vez, para poder revisar cada resultado
        // (GetStatusZip) antes de seguir gastando cupo del set de pruebas. El tipo (factura, nota
        // crédito o nota débito) no lo elige el caller: se decide solo según lo que ya se envió
        // (Client.TestSetSent*) contra lo que la DIAN exige (Client.TestSetRequired*) para este
        // TestSetId — primero se agotan las facturas (para tener algo que referenciar), luego notas
        // débito, luego notas crédito.
        // Envio manual de un documento suelto (boton del portal): resuelve el tipo segun lo que
        // falte y lo manda. El orquestador (RunFullTestSetAsync) no pasa por aqui: el maneja el
        // orden y las rachas por su cuenta.
        public async Task<TestSetSubmissionResult> SendNextTestDocumentAsync(Guid clientId)
        {
            var client = await _dbContext.Clients.FirstOrDefaultAsync(c => c.Id == clientId);
            if (client == null)
                return TestSetSubmissionResult.Failed("Cliente no encontrado.");

            var kind = DetermineNextDocumentKind(client);
            if (kind == null)
                return TestSetSubmissionResult.Failed("Ya se enviaron todos los documentos que exige este set de pruebas. Revisa el resultado de cada uno en el portal de la DIAN.");

            return await SendTestDocumentAsync(clientId, kind.Value, countOnSend: true);
        }

        // countOnSend: si es true incrementa los contadores al transmitir (comportamiento del envio
        // manual). El orquestador lo pasa en false porque solo cuenta los documentos que la DIAN
        // efectivamente ACEPTA, que es lo unico que suma para el set de pruebas.
        // dryRun: arma y firma el documento pero NO lo transmite ni consume consecutivo. Sirve para
        // comprobar que los tres tipos se generan y firman bien antes de gastar intentos reales del
        // set de pruebas, que no se recuperan.
        private async Task<TestSetSubmissionResult> SendTestDocumentAsync(Guid clientId, DocumentKind documentKind, bool countOnSend, bool dryRun = false)
        {
            var client = await _dbContext.Clients.FirstOrDefaultAsync(c => c.Id == clientId);
            if (client == null)
                return TestSetSubmissionResult.Failed("Cliente no encontrado.");

            if (string.IsNullOrWhiteSpace(client.TestSetId))
                return TestSetSubmissionResult.Failed("Este cliente todavía no tiene un TestSetId — registra primero el software propio en el ambiente de habilitación.");

            var resolution = await _dbContext.Resolutions
                .FirstOrDefaultAsync(r => r.ClientId == clientId && r.DocumentType == "FE-TEST" && r.IsActive);
            if (resolution == null)
                return TestSetSubmissionResult.Failed("No hay una resolución de prueba registrada para este cliente.");

            var certificate = await _dbContext.Certificates.FirstOrDefaultAsync(c => c.ClientId == clientId && c.IsActive);
            if (certificate == null)
                return TestSetSubmissionResult.Failed("Este cliente no tiene un certificado digital activo cargado — es requisito para firmar los documentos de prueba.");

            X509Certificate2 cert;
            try
            {
                cert = _cryptoVault.GetCertificate(certificate.FileName, certificate.EncryptedPassword);
            }
            catch (Exception ex)
            {
                return TestSetSubmissionResult.Failed($"No se pudo cargar el certificado: {ex.Message}");
            }

            _logger.LogInformation(
                "Certificado cargado para envio de prueba: Subject={Subject} Issuer={Issuer} NotBefore={NotBefore} NotAfter={NotAfter} HasPrivateKey={HasPrivateKey} Thumbprint={Thumbprint}",
                cert.Subject, cert.Issuer, cert.NotBefore.ToString("u"), cert.NotAfter.ToString("u"), cert.HasPrivateKey, cert.Thumbprint);

            var municipalities = await _dbContext.DianMunicipalities.AsNoTracking().ToDictionaryAsync(m => m.Code);

            try
            {
                // En simulación se "espía" el próximo consecutivo sin reclamarlo: gastar numeración
                // por una prueba en seco dejaría huecos en la secuencia de la resolución.
                var number = dryRun
                    ? (resolution.NextNumber ?? resolution.NumberStart).ToString()
                    : (await ResolutionNumbering.ClaimNextNumberAsync(_dbContext, resolution.Id)).ToString();

                var (filePrefix, ublData) = BuildUblData(documentKind, number, resolution, client, municipalities);

                var xml = _ublGenerator.GenerateInvoiceXml(ublData);
                var cufe = _ublGenerator.CalculateCufe(ublData);
                var certChain = _cryptoVault.GetCertificateChain(certificate.FileName, certificate.EncryptedPassword);
                var signedXml = _xmlSigner.SignXml(xml, cert, certChain);

                if (dryRun)
                {
                    _logger.LogInformation("[SIMULACIÓN] {Kind} {Number} generada y firmada correctamente ({Bytes} bytes). No se transmitió a la DIAN.", documentKind, number, signedXml.Length);
                    return TestSetSubmissionResult.Sent(resolution.Prefix + number, cufe, "Simulación: documento generado y firmado, sin transmitir.");
                }

                var fileSequence = await DianFileNaming.ClaimNextSequenceAsync(_dbContext, clientId);
                var zipFileName = DianFileNaming.BuildFileName("z", client.TaxId, fileSequence);
                var entryFileName = DianFileNaming.BuildFileName(filePrefix, client.TaxId, fileSequence);
                var soapResponse = await _dianSoapClient.SendTestSetAsync(zipFileName, entryFileName, signedXml, client.TestSetId, cert, "2");

                if (countOnSend)
                {
                    RegisterAcceptedDocument(client, documentKind, cufe, resolution.Prefix + number);
                    await _dbContext.SaveChangesAsync();
                }
                else if (documentKind == DocumentKind.Invoice)
                {
                    // Las notas de prueba deben referenciar una factura real, asi que el CUFE y el
                    // numero de la ultima factura se guardan apenas se transmite — sin esperar el
                    // veredicto, porque el orquestador puede necesitarlos antes.
                    client.TestSetLastInvoiceCufe = cufe;
                    client.TestSetLastInvoiceNumber = resolution.Prefix + number;
                    await _dbContext.SaveChangesAsync();
                }

                _logger.LogInformation("Documento de prueba {Kind} {Number} enviado al set de pruebas {TestSetId} del cliente {ClientId}. Respuesta cruda de la DIAN: {SoapResponse}", documentKind, number, client.TestSetId, clientId, soapResponse);

                // El ZipKey es lo unico que devuelve SendTestSetAsync: confirma recepcion, no
                // aceptacion. El veredicto se consulta aparte con GetStatusZip — el anexo tecnico
                // (numeral 7.8.1) exige GetStatusZip y no GetStatus para el flujo asincrono de ZIP.
                var zipKey = System.Text.RegularExpressions.Regex
                    .Match(soapResponse, "<b:ZipKey>([^<]+)</b:ZipKey>").Groups[1].Value;

                if (string.IsNullOrWhiteSpace(zipKey))
                    _logger.LogWarning("La DIAN no devolvio ZipKey para el documento {Kind} {Number} del cliente {ClientId}; no se podra consultar su veredicto.", documentKind, number, clientId);

                return TestSetSubmissionResult.Sent(resolution.Prefix + number, cufe, soapResponse, zipKey);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error enviando documento de prueba para el cliente {ClientId}", clientId);
                return TestSetSubmissionResult.Failed($"Error enviando el documento de prueba: {ex.Message}");
            }
        }

        public async Task<TestSetPreviewResult> PreviewNextTestDocumentAsync(Guid clientId)
        {
            var client = await _dbContext.Clients.FirstOrDefaultAsync(c => c.Id == clientId);
            if (client == null)
                return TestSetPreviewResult.Failed("Cliente no encontrado.");

            if (string.IsNullOrWhiteSpace(client.TestSetId))
                return TestSetPreviewResult.Failed("Este cliente todavía no tiene un TestSetId — registra primero el software propio en el ambiente de habilitación.");

            var documentKind = DetermineNextDocumentKind(client);
            if (documentKind == null)
                return TestSetPreviewResult.Failed("Ya se enviaron todos los documentos que exige este set de pruebas.");

            var resolution = await _dbContext.Resolutions
                .FirstOrDefaultAsync(r => r.ClientId == clientId && r.DocumentType == "FE-TEST" && r.IsActive);
            if (resolution == null)
                return TestSetPreviewResult.Failed("No hay una resolución de prueba registrada para este cliente.");

            var certificate = await _dbContext.Certificates.FirstOrDefaultAsync(c => c.ClientId == clientId && c.IsActive);
            if (certificate == null)
                return TestSetPreviewResult.Failed("Este cliente no tiene un certificado digital activo cargado.");

            X509Certificate2 cert;
            try
            {
                cert = _cryptoVault.GetCertificate(certificate.FileName, certificate.EncryptedPassword);
            }
            catch (Exception ex)
            {
                return TestSetPreviewResult.Failed($"No se pudo cargar el certificado: {ex.Message}");
            }

            var municipalities = await _dbContext.DianMunicipalities.AsNoTracking().ToDictionaryAsync(m => m.Code);

            try
            {
                var peekedNumber = (resolution.NextNumber ?? resolution.NumberStart).ToString();
                var (_, ublData) = BuildUblData(documentKind.Value, peekedNumber, resolution, client, municipalities);

                var xml = _ublGenerator.GenerateInvoiceXml(ublData);
                var cufe = _ublGenerator.CalculateCufe(ublData);
                var certChain = _cryptoVault.GetCertificateChain(certificate.FileName, certificate.EncryptedPassword);
                var signedXml = _xmlSigner.SignXml(xml, cert, certChain);

                return TestSetPreviewResult.Ok(documentKind.Value.ToString(), resolution.Prefix + peekedNumber, cufe, signedXml);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error armando la vista previa del documento de prueba para el cliente {ClientId}", clientId);
                return TestSetPreviewResult.Failed($"Error armando la vista previa: {ex.Message}");
            }
        }

        private static (string FilePrefix, Fel.Core.Models.UblInvoiceData UblData) BuildUblData(
            DocumentKind documentKind, string number, Resolution resolution, Client client,
            IReadOnlyDictionary<string, Fel.Core.Entities.DianMunicipality> municipalities)
        {
            Fel.Core.Models.UblInvoiceData ublData;
            string filePrefix;
            switch (documentKind)
            {
                case DocumentKind.CreditNote:
                    var creditRequest = BuildSyntheticCreditNoteRequest(number, resolution.Prefix, client);
                    ublData = DianDocumentMapper.BuildCreditNoteDataFromRequest(creditRequest, client, resolution, municipalities);
                    filePrefix = DianFileNaming.NotaCredito;
                    break;
                case DocumentKind.DebitNote:
                    var debitRequest = BuildSyntheticDebitNoteRequest(number, resolution.Prefix, client);
                    ublData = DianDocumentMapper.BuildDebitNoteDataFromRequest(debitRequest, client, resolution, municipalities);
                    filePrefix = DianFileNaming.NotaDebito;
                    break;
                default:
                    var invoiceRequest = BuildSyntheticInvoiceRequest(number, resolution.Prefix);
                    ublData = DianDocumentMapper.BuildInvoiceDataFromRequest(invoiceRequest, client, resolution, municipalities);
                    filePrefix = DianFileNaming.FacturaVenta;
                    break;
            }
            ublData.Environment = "2"; // El set de pruebas siempre va a habilitación, sin importar el estado del Client.
            return (filePrefix, ublData);
        }

        // Consulta el resultado de un envío anterior (trackId devuelto dentro de la respuesta SOAP
        // de SendTestSetAsync) — separada de SendNextTestDocumentAsync a propósito, para revisar
        // cada resultado antes de gastar el siguiente cupo del set de pruebas.
        public async Task<TestSetSubmissionResult> GetTestDocumentStatusAsync(Guid clientId, string trackId)
        {
            if (string.IsNullOrWhiteSpace(trackId))
                return TestSetSubmissionResult.Failed("El trackId es requerido.");

            var certificate = await _dbContext.Certificates.FirstOrDefaultAsync(c => c.ClientId == clientId && c.IsActive);
            if (certificate == null)
                return TestSetSubmissionResult.Failed("Este cliente no tiene un certificado digital activo cargado.");

            X509Certificate2 cert;
            try
            {
                cert = _cryptoVault.GetCertificate(certificate.FileName, certificate.EncryptedPassword);
            }
            catch (Exception ex)
            {
                return TestSetSubmissionResult.Failed($"No se pudo cargar el certificado: {ex.Message}");
            }

            try
            {
                var soapResponse = await _dianSoapClient.GetStatusZipAsync(trackId, cert, "2");
                return TestSetSubmissionResult.Sent(trackId, string.Empty, soapResponse);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error consultando estado del trackId {TrackId} para el cliente {ClientId}", trackId, clientId);
                return TestSetSubmissionResult.Failed($"Error consultando el estado: {ex.Message}");
            }
        }

        private enum DocumentKind { Invoice, CreditNote, DebitNote }

        // Primero agota facturas (se necesita al menos una para referenciar en las notas), luego
        // notas débito, luego notas crédito. Null cuando ya se alcanzó el requerido de los tres tipos.
        private static DocumentKind? DetermineNextDocumentKind(Client client)
        {
            if (client.TestSetSentInvoices < client.TestSetRequiredInvoices) return DocumentKind.Invoice;
            if (client.TestSetSentDebitNotes < client.TestSetRequiredDebitNotes) return DocumentKind.DebitNote;
            if (client.TestSetSentCreditNotes < client.TestSetRequiredCreditNotes) return DocumentKind.CreditNote;
            return null;
        }

        // Rachas exigidas por nosotros, mas estrictas que el minimo de la DIAN (que para SoFactory
        // pidio 1 factura aceptada y 0 notas). Una sola aceptacion puede ser suerte; una racha
        // demuestra que el generador y el firmador son estables.
        private const int RequiredInvoiceStreak = 10;
        private const int RequiredNoteStreak = 3;

        // Los intentos del set de pruebas NO se recuperan: si se agotan, la unica salida es volver a
        // registrar el software propio ante la DIAN. Por eso se corta tras unos pocos fallos
        // seguidos en vez de quemar el presupuesto entero contra un bug.
        private const int MaxConsecutiveFailures = 3;

        // Ensayo en seco: genera y firma un documento de cada tipo SIN transmitirlo ni consumir
        // consecutivos ni intentos. Sirve para detectar un problema de generación o de firma antes
        // de soltar el set real, donde cada intento fallido es irrecuperable.
        //
        // Las notas solo se pueden simular si ya existe una factura de prueba a la cual referenciar
        // (la DIAN exige que la nota apunte a un documento real): en un cliente nuevo se simula la
        // factura y se avisa que las notas quedan pendientes.
        public async Task<TestSetSubmissionResult> SimulateTestSetAsync(Guid clientId)
        {
            var client = await _dbContext.Clients.FirstOrDefaultAsync(c => c.Id == clientId);
            if (client == null)
                return TestSetSubmissionResult.Failed("Cliente no encontrado.");

            var tipos = new List<DocumentKind> { DocumentKind.Invoice };
            var notasOmitidas = string.IsNullOrWhiteSpace(client.TestSetLastInvoiceCufe);
            if (!notasOmitidas)
            {
                tipos.Add(DocumentKind.DebitNote);
                tipos.Add(DocumentKind.CreditNote);
            }

            foreach (var tipo in tipos)
            {
                var resultado = await SendTestDocumentAsync(clientId, tipo, countOnSend: false, dryRun: true);
                if (!resultado.IsSuccess)
                    return TestSetSubmissionResult.Failed($"La simulación falló generando {tipo}: {resultado.ErrorMessage}");
            }

            var mensaje = notasOmitidas
                ? "Simulación correcta: la factura se genera y firma bien. Las notas no se simularon porque todavía no hay una factura de prueba que referenciar."
                : $"Simulación correcta: los {tipos.Count} tipos de documento se generan y firman sin errores.";

            _logger.LogInformation("Simulación del set de pruebas del cliente {ClientId}: {Mensaje}", clientId, mensaje);
            return TestSetSubmissionResult.Sent(string.Empty, string.Empty, mensaje);
        }

        // Corre el set de pruebas completo de principio a fin: factura hasta lograr la racha, luego
        // notas debito, luego notas credito. Las notas van despues porque deben referenciar una
        // factura real ya enviada.
        //
        // Pensado para ejecutarse en Fel.Worker y no dentro de una peticion HTTP: cada documento son
        // ~20 segundos entre enviar y obtener el veredicto, asi que el set completo toma minutos.
        // El avance se persiste en el cliente para que el portal lo muestre en vivo.
        public async Task<TestSetSubmissionResult> RunFullTestSetAsync(Guid clientId, CancellationToken ct = default)
        {
            var client = await _dbContext.Clients.FirstOrDefaultAsync(c => c.Id == clientId, ct);
            if (client == null)
                return TestSetSubmissionResult.Failed("Cliente no encontrado.");
            if (string.IsNullOrWhiteSpace(client.TestSetId))
                return TestSetSubmissionResult.Failed("Este cliente todavía no tiene un TestSetId — registra primero el software propio en el ambiente de habilitación.");

            var etapas = new[]
            {
                (Kind: DocumentKind.Invoice,    Streak: RequiredInvoiceStreak, Label: "facturas",      Budget: client.TestSetRequiredInvoices),
                (Kind: DocumentKind.DebitNote,  Streak: RequiredNoteStreak,    Label: "notas débito",  Budget: client.TestSetRequiredDebitNotes),
                (Kind: DocumentKind.CreditNote, Streak: RequiredNoteStreak,    Label: "notas crédito", Budget: client.TestSetRequiredCreditNotes),
            };

            int etapaIndex = 0;
            foreach (var etapa in etapas)
            {
                etapaIndex++;
                int racha = 0, intentos = 0, fallosSeguidos = 0;

                while (racha < etapa.Streak)
                {
                    if (ct.IsCancellationRequested)
                        return await AbortTestSetAsync(client, $"Proceso cancelado durante {etapa.Label}.", ct);

                    if (etapa.Budget > 0 && intentos >= etapa.Budget)
                        return await AbortTestSetAsync(client, $"Se agotaron los {etapa.Budget} intentos disponibles de {etapa.Label} sin lograr {etapa.Streak} aceptadas seguidas.", ct);

                    if (fallosSeguidos >= MaxConsecutiveFailures)
                        return await AbortTestSetAsync(client, $"Se detuvo tras {fallosSeguidos} rechazos seguidos en {etapa.Label}, para no agotar los intentos restantes. Revisa el detalle del último rechazo.", ct);

                    intentos++;
                    var envio = await SendTestDocumentAsync(clientId, etapa.Kind, countOnSend: false);

                    if (!envio.IsSuccess || string.IsNullOrWhiteSpace(envio.TrackId))
                    {
                        fallosSeguidos++;
                        racha = 0;
                        await ReportProgressAsync(client, etapaIndex, etapas.Length, etapa.Label, racha, etapa.Streak,
                            $"No se pudo transmitir: {envio.ErrorMessage}", ct);
                        continue;
                    }

                    var veredicto = await WaitForOutcomeAsync(clientId, envio.TrackId, ct);

                    if (veredicto.Accepted)
                    {
                        racha++;
                        fallosSeguidos = 0;
                        RegisterAcceptedDocument(client, etapa.Kind, envio.Cufe, envio.DocumentNumber);
                        await ReportProgressAsync(client, etapaIndex, etapas.Length, etapa.Label, racha, etapa.Streak,
                            $"{envio.DocumentNumber} aceptada.", ct);
                    }
                    else
                    {
                        racha = 0;
                        fallosSeguidos++;
                        await ReportProgressAsync(client, etapaIndex, etapas.Length, etapa.Label, racha, etapa.Streak,
                            $"{envio.DocumentNumber} rechazada: {veredicto.RejectionSummary}", ct);
                    }
                }

                _logger.LogInformation("Set de pruebas del cliente {ClientId}: etapa {Label} completada con {Streak} aceptadas seguidas en {Intentos} intentos.", clientId, etapa.Label, etapa.Streak, intentos);
            }

            client.DianHabilitationStatus = "Passed";
            client.DianHabilitationProgress = 100;
            client.DianHabilitationMessage = "Set de pruebas superado. El software quedó listo para pasar a producción.";
            await _dbContext.SaveChangesAsync(ct);

            return TestSetSubmissionResult.Sent(string.Empty, string.Empty, client.DianHabilitationMessage);
        }

        // La DIAN valida el ZIP de forma asincrona: primero responde "en proceso de validación" y
        // solo despues entrega el veredicto. Se consulta con pausas hasta que resuelva.
        private async Task<DianStatusOutcome> WaitForOutcomeAsync(Guid clientId, string trackId, CancellationToken ct)
        {
            const int maxIntentos = 12;
            for (int intento = 1; intento <= maxIntentos; intento++)
            {
                if (ct.IsCancellationRequested)
                    return DianStatusOutcome.Pending();

                var status = await GetTestDocumentStatusAsync(clientId, trackId);
                if (status.IsSuccess)
                {
                    var outcome = DianStatusOutcome.FromGetStatusZipResponse(status.DianResponse);
                    if (outcome.Resolved) return outcome;
                }

                if (intento < maxIntentos)
                    await Task.Delay(TimeSpan.FromSeconds(5), ct);
            }

            _logger.LogWarning("La DIAN no resolvió el trackId {TrackId} del cliente {ClientId} tras {Max} consultas.", trackId, clientId, maxIntentos);
            return DianStatusOutcome.Pending();
        }

        private static void RegisterAcceptedDocument(Client client, DocumentKind kind, string cufe, string documentNumber)
        {
            switch (kind)
            {
                case DocumentKind.CreditNote:
                    client.TestSetSentCreditNotes++;
                    break;
                case DocumentKind.DebitNote:
                    client.TestSetSentDebitNotes++;
                    break;
                default:
                    client.TestSetSentInvoices++;
                    client.TestSetLastInvoiceCufe = cufe;
                    client.TestSetLastInvoiceNumber = documentNumber;
                    break;
            }
        }

        private async Task ReportProgressAsync(Client client, int etapa, int totalEtapas, string label, int racha, int requerida, string detalle, CancellationToken ct)
        {
            // El avance combina la etapa con la racha dentro de ella, para que la barra del portal
            // se mueva de verdad y no salte de 0 a 100.
            var avanceEtapa = requerida == 0 ? 1d : (double)racha / requerida;
            client.DianHabilitationProgress = (int)Math.Round(((etapa - 1) + avanceEtapa) / totalEtapas * 100);
            client.DianHabilitationStatus = "Processing";
            client.DianHabilitationMessage = $"Set de pruebas — {label}: {racha}/{requerida} aceptadas seguidas. {detalle}";
            await _dbContext.SaveChangesAsync(ct);
        }

        private async Task<TestSetSubmissionResult> AbortTestSetAsync(Client client, string motivo, CancellationToken ct)
        {
            client.DianHabilitationStatus = "Failed";
            client.DianHabilitationMessage = motivo;
            await _dbContext.SaveChangesAsync(ct);
            _logger.LogWarning("Set de pruebas del cliente {ClientId} abortado: {Motivo}", client.Id, motivo);
            return TestSetSubmissionResult.Failed(motivo);
        }

        // Datos sintéticos válidos pero sin significado real de negocio — a la DIAN, en el set de
        // pruebas, solo le importa que el XML sea estructuralmente correcto y que el CUFE calce.
        private static InvoiceRequest BuildSyntheticInvoiceRequest(string documentNumber, string prefix)
        {
            const decimal unitPrice = 100000m;
            const decimal taxRate = 19m;
            var taxAmount = Math.Round(unitPrice * taxRate / 100m, 2);

            return new InvoiceRequest
            {
                DocumentNumber = documentNumber,
                Prefix = prefix,
                IssueDate = Fel.Core.Models.ColombiaTime.Now,
                Currency = "COP",
                TotalAmount = unitPrice + taxAmount,
                Customer = new CustomerData
                {
                    TaxId = "222222222222",
                    IdentificationCode = "13",
                    Name = "Cliente de pruebas DIAN",
                    Email = "pruebas@facil-factura.pro",
                    DepartmentCode = "11",
                    DepartmentName = "Bogotá, D.C.",
                    CityCode = "11001",
                    CityName = "Bogotá D.C.",
                    Address = "Cra 1 # 1-1"
                },
                Lines = new List<InvoiceLine>
                {
                    new InvoiceLine
                    {
                        ItemCode = "TEST-001",
                        Description = "Producto de prueba habilitación DIAN",
                        Quantity = 1,
                        UnitPrice = unitPrice,
                        LineExtensionAmount = unitPrice,
                        // Cada línea necesita su propio TaxTotal (ver BaseUblStrategy.BuildLine) —
                        // antes solo se informaba el impuesto a nivel de cabecera.
                        Taxes = new List<TaxSubtotal>
                        {
                            new TaxSubtotal { TaxId = "01", TaxableAmount = unitPrice, TaxAmount = taxAmount, Percent = taxRate }
                        }
                    }
                },
                Taxes = new List<TaxSubtotal>
                {
                    new TaxSubtotal { TaxId = "01", TaxableAmount = unitPrice, TaxAmount = taxAmount, Percent = taxRate }
                },
                PaymentMeans = new List<PaymentMeansData>
                {
                    new PaymentMeansData { Id = "1", PaymentMeansCode = "10" }
                }
            };
        }

        // Nota crédito sintética — referencia la última factura de prueba enviada (Client.TestSetLast*)
        // cuando existe; si todavía no se ha enviado ninguna factura en este set, se arma como no
        // referenciada (la DIAN también acepta ese caso, ver DianDocumentMapper.BuildCreditNoteDataFromRequest).
        private static CreditNoteRequest BuildSyntheticCreditNoteRequest(string documentNumber, string prefix, Client client)
        {
            var baseRequest = BuildSyntheticInvoiceRequest(documentNumber, prefix);
            return new CreditNoteRequest
            {
                DocumentNumber = baseRequest.DocumentNumber,
                Prefix = baseRequest.Prefix,
                IssueDate = baseRequest.IssueDate,
                Currency = baseRequest.Currency,
                TotalAmount = baseRequest.TotalAmount,
                Customer = baseRequest.Customer,
                Lines = baseRequest.Lines,
                Taxes = baseRequest.Taxes,
                PaymentMeans = baseRequest.PaymentMeans,
                DiscrepancyResponseCode = "2",
                DiscrepancyDescription = "Anulación de prueba — set de pruebas habilitación DIAN",
                BillingReferenceCufe = client.TestSetLastInvoiceCufe ?? string.Empty,
                BillingReferenceDocumentNumber = client.TestSetLastInvoiceNumber ?? string.Empty,
                BillingReferenceDate = string.IsNullOrEmpty(client.TestSetLastInvoiceCufe) ? null : DateTime.UtcNow
            };
        }

        // Nota débito sintética — mismos datos que la nota crédito (DebitNoteRequest no agrega
        // campos propios), solo cambia el DianCode que asigna DianDocumentMapper.BuildDebitNoteDataFromRequest.
        private static DebitNoteRequest BuildSyntheticDebitNoteRequest(string documentNumber, string prefix, Client client)
        {
            var credit = BuildSyntheticCreditNoteRequest(documentNumber, prefix, client);
            return new DebitNoteRequest
            {
                DocumentNumber = credit.DocumentNumber,
                Prefix = credit.Prefix,
                IssueDate = credit.IssueDate,
                Currency = credit.Currency,
                TotalAmount = credit.TotalAmount,
                Customer = credit.Customer,
                Lines = credit.Lines,
                Taxes = credit.Taxes,
                PaymentMeans = credit.PaymentMeans,
                DiscrepancyResponseCode = "1",
                DiscrepancyDescription = "Ajuste de prueba — set de pruebas habilitación DIAN",
                BillingReferenceCufe = credit.BillingReferenceCufe,
                BillingReferenceDocumentNumber = credit.BillingReferenceDocumentNumber,
                BillingReferenceDate = credit.BillingReferenceDate
            };
        }
    }

    public class TestSetSubmissionResult
    {
        // OJO: IsSuccess significa que la DIAN RECIBIO el documento (devolvio un ZipKey), no que lo
        // haya aceptado. La aceptacion se consulta despues con TrackId y se interpreta con
        // DianStatusOutcome — durante la habilitacion de SoFactory hubo seis documentos recibidos
        // correctamente y rechazados todos con la regla ZE02.
        public bool IsSuccess { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public string DocumentNumber { get; set; } = string.Empty;
        public string Cufe { get; set; } = string.Empty;
        public string DianResponse { get; set; } = string.Empty;

        // ZipKey devuelto por SendTestSetAsync, necesario para consultar el veredicto con GetStatusZip.
        public string TrackId { get; set; } = string.Empty;

        public static TestSetSubmissionResult Failed(string message) => new() { IsSuccess = false, ErrorMessage = message };

        public static TestSetSubmissionResult Sent(string documentNumber, string cufe, string dianResponse, string trackId = "") =>
            new() { IsSuccess = true, DocumentNumber = documentNumber, Cufe = cufe, DianResponse = dianResponse, TrackId = trackId };
    }

    public class TestSetPreviewResult
    {
        public bool IsSuccess { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public string DocumentKind { get; set; } = string.Empty;
        public string DocumentNumber { get; set; } = string.Empty;
        public string Cufe { get; set; } = string.Empty;
        public string SignedXml { get; set; } = string.Empty;

        public static TestSetPreviewResult Failed(string message) => new() { IsSuccess = false, ErrorMessage = message };

        public static TestSetPreviewResult Ok(string documentKind, string documentNumber, string cufe, string signedXml) =>
            new() { IsSuccess = true, DocumentKind = documentKind, DocumentNumber = documentNumber, Cufe = cufe, SignedXml = signedXml };
    }
}
