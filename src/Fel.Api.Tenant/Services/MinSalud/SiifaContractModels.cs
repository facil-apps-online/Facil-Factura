using System.Text.Json.Serialization;

namespace Fel.Api.Tenant.Services.MinSalud
{
    // Modelos del contrato real documentado en "Manual de Consumo API-Docker-FEV-RIPS v4.3"
    // (MinSalud, noviembre 2025) — POST /api/Auth/LoginSISPRO y POST
    // /api/PaquetesFevRips/CargarRipsSinFactura. El nombre exacto del campo con el token en la
    // respuesta de LoginSISPRO no quedó legible en el manual (se perdió en la extracción del PDF
    // escaneado) — queda como "token" por convención, a verificar contra una respuesta real.

    public class SiifaLoginRequest
    {
        [JsonPropertyName("persona")]
        public SiifaPersona Persona { get; set; } = new();

        [JsonPropertyName("clave")]
        public string Clave { get; set; } = string.Empty;

        [JsonPropertyName("nit")]
        public string Nit { get; set; } = string.Empty;

        // Opcional: solo aplica a profesionales independientes obligados a facturar
        // electrónicamente cuando el número de documento es distinto del NIT asignado por la DIAN.
        [JsonPropertyName("tipoUsuario")]
        public string? TipoUsuario { get; set; }
    }

    public class SiifaPersona
    {
        [JsonPropertyName("identificacion")]
        public SiifaIdentificacion Identificacion { get; set; } = new();
    }

    public class SiifaIdentificacion
    {
        [JsonPropertyName("tipo")]
        public string Tipo { get; set; } = string.Empty; // CC, RE, PIN, PINx, PIE

        [JsonPropertyName("numero")]
        public string Numero { get; set; } = string.Empty;
    }

    public class SiifaLoginResponse
    {
        [JsonPropertyName("token")]
        public string? Token { get; set; }
    }

    // Petición de PaquetesFevRips/CargarRipsSinFactura (y, con xmlFevFile no vacío, CargarFevRips).
    public class SiifaCargarRipsRequest
    {
        [JsonPropertyName("rips")]
        public MuvRipsRoot Rips { get; set; } = new();

        [JsonPropertyName("xmlFevFile")]
        public string XmlFevFile { get; set; } = string.Empty; // Vacío en CargarRipsSinFactura
    }

    // Respuesta común a los métodos de transmisión de documentos (sección "8.13. Response" del manual).
    public class SiifaSubmissionResponse
    {
        [JsonPropertyName("ResultState")]
        public bool ResultState { get; set; }

        [JsonPropertyName("PorcesoID")]
        public int ProcesoId { get; set; }

        [JsonPropertyName("NumFactura")]
        public string? NumFactura { get; set; }

        [JsonPropertyName("CodigoUnicoValidacion")]
        public string? CodigoUnicoValidacion { get; set; } // CUV, 96 hex — solo si ResultState = true

        [JsonPropertyName("FechaRadicacion")]
        public string? FechaRadicacion { get; set; }

        [JsonPropertyName("Ambiente")]
        public string? Ambiente { get; set; } // DockerTest, DockerStage, DockerProd

        [JsonPropertyName("Modulo")]
        public string? Modulo { get; set; }

        [JsonPropertyName("ResultadosValidacion")]
        public List<SiifaResultadoValidacion>? ResultadosValidacion { get; set; }
    }

    public class SiifaResultadoValidacion
    {
        [JsonPropertyName("Codigo")]
        public string? Codigo { get; set; }

        [JsonPropertyName("Descripcion")]
        public string? Descripcion { get; set; }

        [JsonPropertyName("Observaciones")]
        public string? Observaciones { get; set; }

        [JsonPropertyName("Fuente")]
        public string? Fuente { get; set; } // RIPS, FacturaElectronica, NotaCredito, NotaDebito
    }
}
