using System.Net.Http.Json;
using Fel.Api.Tenant.DTOs;
using Fel.Core.Entities;
using Fel.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace Fel.Api.Tenant.Services.MinSalud
{
    // Cliente real del contrato documentado en el "Manual de Consumo API-Docker-FEV-RIPS v4.3"
    // (MinSalud, noviembre 2025): LoginSISPRO + CargarRipsSinFactura. A diferencia de la DIAN, este
    // no es un servicio en la nube del Ministerio — es un contenedor Docker que el prestador (o
    // nosotros en su nombre) despliega en su propia infraestructura; el Ministerio entrega la
    // imagen directamente al prestador habilitado, así que no hay URL pública fija.
    //
    // Pendiente de verificar contra una instancia real: (1) el nombre exacto del campo del token en
    // la respuesta de LoginSISPRO (el manual no lo dejó legible), (2) si Content-Encoding: gzip es
    // obligatorio o solo una opción de rendimiento — por ahora se envía sin comprimir.
    public class MinSaludMuvService : IMinSaludMuvService
    {
        private readonly HttpClient _httpClient;
        private readonly ICryptoService _cryptoService;
        private readonly ILogger<MinSaludMuvService> _logger;
        private readonly Microsoft.Extensions.Configuration.IConfiguration _configuration;

        public MinSaludMuvService(
            HttpClient httpClient,
            ICryptoService cryptoService,
            ILogger<MinSaludMuvService> logger,
            Microsoft.Extensions.Configuration.IConfiguration configuration)
        {
            _httpClient = httpClient;
            _cryptoService = cryptoService;
            _logger = logger;
            _configuration = configuration;
        }

        public async Task<(bool IsSuccess, string TrackingId, string Message, string JsonPayload)> SendRipsAsync(RipsEmitRequest request, Client client, Fel.Infrastructure.Services.MinSaludCredentials credentials)
        {
            // MinSalud no ofrece sandbox público (ver "Manual de Consumo API-Docker-FEV-RIPS"):
            // el contenedor real solo se puede probar tras la habilitación formal como PSS/PTS
            // ante SISPRO. Los Clients de prueba de developers (IsDeveloperSandbox=true) nunca van
            // a tener ese trámite hecho, así que en vez de fallar siempre con "no configurado" se
            // simula la respuesta de éxito que MinSalud devolvería (ver numeral 8.13 del manual).
            if (client.IsDeveloperSandbox)
            {
                var muvRootSandbox = MapToMuvRips(request);
                var jsonPayloadSandbox = System.Text.Json.JsonSerializer.Serialize(muvRootSandbox, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                var fakeCuv = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(48)).ToLowerInvariant();
                return (true, fakeCuv, "CUV generado exitosamente (respuesta simulada — Client de prueba sin habilitación real ante MinSalud).", jsonPayloadSandbox);
            }

            // El ambiente decide TODO: contra cuál MUV se emite y con qué credenciales. Las de
            // pruebas no sirven en producción ni al revés, por eso se guardan por separado.
            var esProduccion = credentials.Environment == MinSaludEnvironments.Production;
            var nombreAmbiente = esProduccion ? "producción" : "pruebas";

            // La URL es configuración de la plataforma, no un dato por cliente: la instalación del
            // MUV es una sola y es nuestra. Cambiarla es una operación de infraestructura.
            var urlEfectiva = _configuration[esProduccion ? "MinSalud:MuvProductionUrl" : "MinSalud:MuvTestUrl"];

            if (string.IsNullOrWhiteSpace(urlEfectiva))
            {
                return (false, string.Empty, $"No hay URL del MUV configurada para el ambiente de {nombreAmbiente} (MinSalud:Muv{(esProduccion ? "Production" : "Test")}Url).", string.Empty);
            }

            var identificacionTipo = esProduccion ? credentials.IdentificationType : credentials.TestIdentificationType;
            var identificacionNumero = esProduccion ? credentials.IdentificationNumber : credentials.TestIdentificationNumber;
            var claveCifrada = esProduccion ? credentials.PasswordEncrypted : credentials.TestPasswordEncrypted;

            if (string.IsNullOrWhiteSpace(identificacionNumero) || string.IsNullOrWhiteSpace(claveCifrada))
            {
                return (false, string.Empty, $"Este emisor no tiene configuradas las credenciales del prestador ante el MUV-FEV-RIPS para el ambiente de {nombreAmbiente}.", string.Empty);
            }

            var muvRoot = MapToMuvRips(request);
            var jsonPayload = System.Text.Json.JsonSerializer.Serialize(muvRoot, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            var baseUrl = urlEfectiva.TrimEnd('/');

            try
            {
                var loginRequest = new SiifaLoginRequest
                {
                    Persona = new SiifaPersona
                    {
                        Identificacion = new SiifaIdentificacion
                        {
                            // Tipo de DOCUMENTO. Antes acá llegaban también RE, PIN, PINx y PIE,
                            // que no son tipos de documento sino de usuario: van en TipoUsuario.
                            Tipo = identificacionTipo ?? "CC",
                            Numero = identificacionNumero
                        }
                    },
                    Clave = _cryptoService.Decrypt(claveCifrada),
                    Nit = client.TaxId,
                    // Campo declarado desde el principio pero que nunca se enviaba. El manual lo
                    // marca opcional, y para PSS/PTS debe ir en RE.
                    TipoUsuario = credentials.UserType
                };

                var loginResponse = await _httpClient.PostAsJsonAsync($"{baseUrl}/api/Auth/LoginSISPRO", loginRequest);
                loginResponse.EnsureSuccessStatusCode();
                var loginResult = await loginResponse.Content.ReadFromJsonAsync<SiifaLoginResponse>();

                if (string.IsNullOrWhiteSpace(loginResult?.Token))
                {
                    return (false, string.Empty, "El MUV-FEV-RIPS no devolvió un token de autenticación válido.", jsonPayload);
                }

                using var submitRequest = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/api/PaquetesFevRips/CargarRipsSinFactura")
                {
                    Content = JsonContent.Create(new SiifaCargarRipsRequest { Rips = muvRoot, XmlFevFile = string.Empty })
                };
                submitRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", loginResult.Token);

                var submitResponse = await _httpClient.SendAsync(submitRequest);
                submitResponse.EnsureSuccessStatusCode();
                var result = await submitResponse.Content.ReadFromJsonAsync<SiifaSubmissionResponse>();

                if (result == null)
                {
                    return (false, string.Empty, "El MUV-FEV-RIPS devolvió una respuesta vacía.", jsonPayload);
                }

                if (!result.ResultState)
                {
                    var errores = result.ResultadosValidacion != null
                        ? string.Join("; ", result.ResultadosValidacion.Select(r => r.Observaciones ?? r.Descripcion))
                        : "Rechazado por el mecanismo único de validación.";
                    return (false, string.Empty, errores, jsonPayload);
                }

                return (true, result.CodigoUnicoValidacion ?? string.Empty, "CUV generado exitosamente.", jsonPayload);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error de integración con el MUV-FEV-RIPS para el emisor {ClientId}", client.Id);
                return (false, string.Empty, $"Error de integración MUV-FEV-RIPS: {ex.Message}", jsonPayload);
            }
        }

        private static MuvRipsRoot MapToMuvRips(RipsEmitRequest request)
        {
            // CargarRipsSinFactura exige numFactura vacío y el par tipoNota/numNota en su lugar (ver
            // ejemplo del manual: "tipoNota": "RS"). NumNota se genera localmente como identificador
            // de esta radicación, ya que no viene atada a ningún número de factura.
            var root = new MuvRipsRoot
            {
                NumDocumentoIdObligado = request.ProviderCode,
                NumFactura = null,
                TipoNota = "RS",
                NumNota = $"RS{DateTime.UtcNow:yyyyMMddHHmmss}",
                Usuarios = new List<MuvUsuario>()
            };

            // Todo dato clínico y demográfico sale del request. Antes varios iban quemados —
            // régimen contributivo, residencia en Bogotá, zona urbana, fecha de hoy, CUPS 890201,
            // enfermedad general, diagnóstico Z000 — de modo que cualquier prestador reportaba
            // exactamente lo mismo sin importar a quién hubiera atendido. El MUV los aceptaba
            // porque son códigos válidos, así que el error no se notaba: el reporte simplemente
            // era falso. Si un dato falta ahora, la petición se rechaza en la validación del DTO.
            var usuario = new MuvUsuario
            {
                TipoDocumentoIdentificacion = request.Patient.IdentificationType,
                NumDocumentoIdentificacion = request.Patient.IdentificationNumber,
                TipoUsuario = request.Patient.UserType,
                FechaNacimiento = request.Patient.BirthDate.ToString("yyyy-MM-dd"),
                CodSexo = request.Patient.BiologicalSex,
                CodPaisResidencia = request.Patient.ResidenceCountryCode,
                CodMunicipioResidencia = request.Patient.ResidenceMunicipalityCode,
                CodZonaTerritorialResidencia = request.Patient.ResidenceZoneCode,
                Consecutivo = 1,
                Consultas = new List<MuvConsulta>(),
                Procedimientos = new List<MuvProcedimiento>()
            };

            int consecutivoConsulta = 1;
            foreach (var cons in request.Consultations)
            {
                usuario.Consultas.Add(new MuvConsulta
                {
                    CodPrestador = request.ProviderCode,
                    FechaInicioAtencion = cons.ServiceDate!.Value.ToString("yyyy-MM-dd HH:mm"),
                    NumAutorizacion = cons.AuthorizationNumber,
                    CodConsulta = cons.ConsultationCode,
                    ModalidadGrupoServicioTecSal = cons.ServiceModalityCode,
                    GrupoServicios = cons.ServiceGroupCode,
                    CodServicio = cons.ServiceCode!.Value,
                    FinalidadTecnologiaSalud = cons.PurposeCode,
                    CausaMotivoAtencion = cons.AttentionCauseCode,
                    CodDiagnosticoPrincipal = cons.MainDiagnosisCode,
                    // CIE-11 (Res 948 de 2026): aditivo al de CIE-10. Si el prestador no lo informa
                    // viaja en null, que es lo que la norma exige — la llave debe estar presente.
                    CodDiagnosticoPrincipalCIE11 = cons.MainDiagnosisCodeCie11,
                    NomCodDiagnosticoPrincipalCIE11 = cons.MainDiagnosisNameCie11,
                    TipoDiagnosticoPrincipal = cons.MainDiagnosisType,
                    TipoDocumentoIdentificacion = request.Patient.IdentificationType,
                    NumDocumentoIdentificacion = request.Patient.IdentificationNumber,
                    VrServicio = cons.ServiceValue!.Value,
                    ConceptoRecaudo = cons.CollectionConceptCode,
                    ValorPagoModerador = cons.CopayAmount,
                    NumFEVPagoModerador = cons.ModeratorPaymentInvoiceNumber,
                    Consecutivo = consecutivoConsulta++
                });
            }

            int consecutivoProc = 1;
            foreach (var proc in request.Procedures)
            {
                usuario.Procedimientos.Add(new MuvProcedimiento
                {
                    CodPrestador = request.ProviderCode,
                    FechaInicioAtencion = proc.ServiceDate!.Value.ToString("yyyy-MM-dd HH:mm"),
                    IdMIPRES = proc.MipresId,
                    NumAutorizacion = proc.AuthorizationNumber,
                    CodProcedimiento = proc.ProcedureCode,
                    ViaIngresoServicioSalud = proc.EntryRouteCode,
                    ModalidadGrupoServicioTecSal = proc.ServiceModalityCode,
                    GrupoServicios = proc.ServiceGroupCode,
                    CodServicio = proc.ServiceCode!.Value,
                    FinalidadTecnologiaSalud = proc.PurposeCode,
                    TipoDocumentoIdentificacion = request.Patient.IdentificationType,
                    NumDocumentoIdentificacion = request.Patient.IdentificationNumber,
                    CodDiagnosticoPrincipal = proc.MainDiagnosisCode,
                    // CIE-11 (Res 948 de 2026): aditivo, viaja en null si no se informa.
                    CodDiagnosticoPrincipalCIE11 = proc.MainDiagnosisCodeCie11,
                    NomCodDiagnosticoPrincipalCIE11 = proc.MainDiagnosisNameCie11,
                    CodComplicacion = proc.ComplicationCode,
                    VrServicio = proc.ServiceValue!.Value,
                    ConceptoRecaudo = proc.CollectionConceptCode,
                    ValorPagoModerador = proc.CopayAmount,
                    Consecutivo = consecutivoProc++
                });
            }

            root.Usuarios.Add(usuario);
            return root;
        }
    }
}
