using System.Text.Json;
using Fel.Api.Tenant.DTOs;
using Microsoft.Extensions.Logging;

namespace Fel.Api.Tenant.Services.MinSalud
{
    public class MinSaludMuvService : IMinSaludMuvService
    {
        private readonly ILogger<MinSaludMuvService> _logger;

        public MinSaludMuvService(ILogger<MinSaludMuvService> logger)
        {
            _logger = logger;
        }

        public async Task<(bool IsSuccess, string TrackingId, string Message, string JsonPayload)> SendRipsAsync(RipsEmitRequest request)
        {
            try
            {
                var muvRoot = MapToMuvRips(request);
                var jsonPayload = JsonSerializer.Serialize(muvRoot, new JsonSerializerOptions { WriteIndented = true });

                _logger.LogInformation("JSON MUV generado exitosamente. Enviando a MinSalud...");
                
                // TODO: Aquí iría el HttpClient.PostAsync hacia el endpoint oficial del MUV del Ministerio.
                // Simulamos un retraso de red
                await Task.Delay(500);

                var trackingId = Guid.NewGuid().ToString("N");
                return (true, trackingId, "CUV generado exitosamente (Simulado)", jsonPayload);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error mapeando o enviando RIPS al MUV");
                return (false, string.Empty, $"Error de integración MUV: {ex.Message}", string.Empty);
            }
        }

        private MuvRipsRoot MapToMuvRips(RipsEmitRequest request)
        {
            var root = new MuvRipsRoot
            {
                NumDocumentoIdObligado = request.ProviderCode,
                NumFactura = "SS-STANDALONE", // Al ser independiente de FEV, se usa un identificador temporal o exento
                Usuarios = new List<MuvUsuario>()
            };

            var usuario = new MuvUsuario
            {
                TipoDocumentoIdentificacion = request.Patient.IdentificationType,
                NumDocumentoIdentificacion = request.Patient.IdentificationNumber,
                TipoUsuario = "01", // Contributivo por defecto (esto debería venir en el request)
                FechaNacimiento = request.Patient.BirthDate.ToString("yyyy-MM-dd"),
                CodSexo = request.Patient.BiologicalSex,
                CodPaisResidencia = "170",
                CodMunicipioResidencia = "11001", // Bogota by default (should come from request)
                CodZonaTerritorialResidencia = "01", // Urbana
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
                    FechaInicioAtencion = DateTime.Now.ToString("yyyy-MM-dd HH:mm"), // Should come from request
                    CodConsulta = "890201", // Should come from request, hardcoded for now if not provided
                    ModalidadGrupoServicioTecSal = "01",
                    GrupoServicios = "01",
                    CodServicio = 1,
                    FinalidadTecnologiaSalud = cons.PurposeCode,
                    CausaMotivoAtencion = "13", // Enfermedad general
                    CodDiagnosticoPrincipal = cons.MainDiagnosisCode,
                    TipoDiagnosticoPrincipal = "01", // Impresion diagnostica
                    TipoDocumentoIdentificacion = request.Patient.IdentificationType,
                    NumDocumentoIdentificacion = request.Patient.IdentificationNumber,
                    VrServicio = cons.CopayAmount, // A simplificacion
                    ConceptoRecaudo = "01", // Copago
                    ValorPagoModerador = cons.CopayAmount,
                    Consecutivo = consecutivoConsulta++
                });
            }

            int consecutivoProc = 1;
            foreach (var proc in request.Procedures)
            {
                usuario.Procedimientos.Add(new MuvProcedimiento
                {
                    CodPrestador = request.ProviderCode,
                    FechaInicioAtencion = DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
                    CodProcedimiento = proc.ProcedureCode,
                    ViaIngresoServicioSalud = "01",
                    ModalidadGrupoServicioTecSal = "01",
                    GrupoServicios = "01",
                    CodServicio = 1,
                    FinalidadTecnologiaSalud = proc.PurposeCode,
                    TipoDocumentoIdentificacion = request.Patient.IdentificationType,
                    NumDocumentoIdentificacion = request.Patient.IdentificationNumber,
                    CodDiagnosticoPrincipal = "Z000", // Required, should come from request
                    VrServicio = 0,
                    ConceptoRecaudo = "05", // No aplica
                    ValorPagoModerador = 0,
                    Consecutivo = consecutivoProc++
                });
            }

            root.Usuarios.Add(usuario);
            return root;
        }
    }
}
