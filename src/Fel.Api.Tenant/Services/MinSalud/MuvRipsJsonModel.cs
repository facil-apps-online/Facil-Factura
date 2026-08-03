using System.Text.Json.Serialization;

namespace Fel.Api.Tenant.Services.MinSalud
{
    /// <summary>
    /// Modelo raíz exigido por el Mecanismo Único de Validación (MUV) del Ministerio de Salud (Res 2275).
    /// </summary>
    public class MuvRipsRoot
    {
        [JsonPropertyName("numDocumentoIdObligado")]
        public string NumDocumentoIdObligado { get; set; } = string.Empty;

        [JsonPropertyName("numFactura")]
        public string NumFactura { get; set; } = string.Empty;

        [JsonPropertyName("tipoNota")]
        public string? TipoNota { get; set; }

        [JsonPropertyName("numNota")]
        public string? NumNota { get; set; }

        [JsonPropertyName("usuarios")]
        public List<MuvUsuario> Usuarios { get; set; } = new();
    }

    public class MuvUsuario
    {
        [JsonPropertyName("tipoDocumentoIdentificacion")]
        public string TipoDocumentoIdentificacion { get; set; } = string.Empty;

        [JsonPropertyName("numDocumentoIdentificacion")]
        public string NumDocumentoIdentificacion { get; set; } = string.Empty;

        [JsonPropertyName("tipoUsuario")]
        public string TipoUsuario { get; set; } = string.Empty;

        [JsonPropertyName("fechaNacimiento")]
        public string FechaNacimiento { get; set; } = string.Empty; // Format YYYY-MM-DD

        [JsonPropertyName("codSexo")]
        public string CodSexo { get; set; } = string.Empty;

        [JsonPropertyName("codPaisResidencia")]
        public string CodPaisResidencia { get; set; } = "170"; // Colombia by default

        [JsonPropertyName("codMunicipioResidencia")]
        public string CodMunicipioResidencia { get; set; } = string.Empty;

        [JsonPropertyName("codZonaTerritorialResidencia")]
        public string CodZonaTerritorialResidencia { get; set; } = string.Empty;

        [JsonPropertyName("consecutivo")]
        public int Consecutivo { get; set; }

        // Arrays de servicios
        [JsonPropertyName("consultas")]
        public List<MuvConsulta>? Consultas { get; set; }

        [JsonPropertyName("procedimientos")]
        public List<MuvProcedimiento>? Procedimientos { get; set; }
    }

    public class MuvConsulta
    {
        [JsonPropertyName("codPrestador")]
        public string CodPrestador { get; set; } = string.Empty;

        [JsonPropertyName("fechaInicioAtencion")]
        public string FechaInicioAtencion { get; set; } = string.Empty; // Format YYYY-MM-DD HH:MM

        [JsonPropertyName("numAutorizacion")]
        public string? NumAutorizacion { get; set; }

        [JsonPropertyName("codConsulta")]
        public string CodConsulta { get; set; } = string.Empty;

        [JsonPropertyName("modalidadGrupoServicioTecSal")]
        public string ModalidadGrupoServicioTecSal { get; set; } = string.Empty;

        [JsonPropertyName("grupoServicios")]
        public string GrupoServicios { get; set; } = string.Empty;

        [JsonPropertyName("codServicio")]
        public int CodServicio { get; set; }

        [JsonPropertyName("finalidadTecnologiaSalud")]
        public string FinalidadTecnologiaSalud { get; set; } = string.Empty;

        [JsonPropertyName("causaMotivoAtencion")]
        public string CausaMotivoAtencion { get; set; } = string.Empty;

        [JsonPropertyName("codDiagnosticoPrincipal")]
        public string CodDiagnosticoPrincipal { get; set; } = string.Empty;

        [JsonPropertyName("codDiagnosticoRelacionado1")]
        public string? CodDiagnosticoRelacionado1 { get; set; }

        [JsonPropertyName("codDiagnosticoRelacionado2")]
        public string? CodDiagnosticoRelacionado2 { get; set; }

        [JsonPropertyName("codDiagnosticoRelacionado3")]
        public string? CodDiagnosticoRelacionado3 { get; set; }

        [JsonPropertyName("tipoDiagnosticoPrincipal")]
        public string TipoDiagnosticoPrincipal { get; set; } = string.Empty;

        [JsonPropertyName("tipoDocumentoIdentificacion")]
        public string TipoDocumentoIdentificacion { get; set; } = string.Empty;

        [JsonPropertyName("numDocumentoIdentificacion")]
        public string NumDocumentoIdentificacion { get; set; } = string.Empty;

        [JsonPropertyName("vrServicio")]
        public decimal VrServicio { get; set; }

        [JsonPropertyName("conceptoRecaudo")]
        public string ConceptoRecaudo { get; set; } = string.Empty;

        [JsonPropertyName("valorPagoModerador")]
        public decimal ValorPagoModerador { get; set; }

        [JsonPropertyName("numFEVPagoModerador")]
        public string? NumFEVPagoModerador { get; set; }

        [JsonPropertyName("consecutivo")]
        public int Consecutivo { get; set; }
    }

    public class MuvProcedimiento
    {
        [JsonPropertyName("codPrestador")]
        public string CodPrestador { get; set; } = string.Empty;

        [JsonPropertyName("fechaInicioAtencion")]
        public string FechaInicioAtencion { get; set; } = string.Empty; // Format YYYY-MM-DD HH:MM

        [JsonPropertyName("idMIPRES")]
        public string? IdMIPRES { get; set; }

        [JsonPropertyName("numAutorizacion")]
        public string? NumAutorizacion { get; set; }

        [JsonPropertyName("codProcedimiento")]
        public string CodProcedimiento { get; set; } = string.Empty;

        [JsonPropertyName("viaIngresoServicioSalud")]
        public string ViaIngresoServicioSalud { get; set; } = string.Empty;

        [JsonPropertyName("modalidadGrupoServicioTecSal")]
        public string ModalidadGrupoServicioTecSal { get; set; } = string.Empty;

        [JsonPropertyName("grupoServicios")]
        public string GrupoServicios { get; set; } = string.Empty;

        [JsonPropertyName("codServicio")]
        public int CodServicio { get; set; }

        [JsonPropertyName("finalidadTecnologiaSalud")]
        public string FinalidadTecnologiaSalud { get; set; } = string.Empty;

        [JsonPropertyName("tipoDocumentoIdentificacion")]
        public string TipoDocumentoIdentificacion { get; set; } = string.Empty;

        [JsonPropertyName("numDocumentoIdentificacion")]
        public string NumDocumentoIdentificacion { get; set; } = string.Empty;

        [JsonPropertyName("codDiagnosticoPrincipal")]
        public string CodDiagnosticoPrincipal { get; set; } = string.Empty;

        [JsonPropertyName("codDiagnosticoRelacionado")]
        public string? CodDiagnosticoRelacionado { get; set; }

        [JsonPropertyName("codComplicacion")]
        public string? CodComplicacion { get; set; }

        [JsonPropertyName("vrServicio")]
        public decimal VrServicio { get; set; }

        [JsonPropertyName("conceptoRecaudo")]
        public string ConceptoRecaudo { get; set; } = string.Empty;

        [JsonPropertyName("valorPagoModerador")]
        public decimal ValorPagoModerador { get; set; }

        [JsonPropertyName("numFEVPagoModerador")]
        public string? NumFEVPagoModerador { get; set; }

        [JsonPropertyName("consecutivo")]
        public int Consecutivo { get; set; }
    }
}
