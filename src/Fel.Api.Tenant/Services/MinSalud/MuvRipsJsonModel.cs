using System.Text.Json.Serialization;

namespace Fel.Api.Tenant.Services.MinSalud
{
    /// <summary>
    /// Modelo raíz exigido por el Mecanismo Único de Validación (MUV) del Ministerio de Salud.
    /// </summary>
    /// <remarks>
    /// Norma vigente: <b>Resolución 948 del 14 de mayo de 2026</b>, que derogó la 2275 de 2023 (contra
    /// la que estaba escrito este modelo originalmente), la 558 y la 1884 de 2024. La especificación
    /// de campos es el "Documento Técnico 1", versión 003 del 15 de julio de 2026.
    ///
    /// La 948 agregó los campos de CIE-11, que son <b>aditivos</b>: conviven con los de CIE-10 y no
    /// los reemplazan. Admiten longitud cero, pero la llave debe existir en el JSON — cuando no hay
    /// dato se informa null (sin comillas). Por eso se declaran como string? y NO se marcan con
    /// JsonIgnore: System.Text.Json serializa los nulos por defecto y así la llave viaja presente,
    /// que es justamente el "ajuste de versión de software de carácter estructural" que exige la norma.
    ///
    /// Pendiente: de las siete secciones del RIPS solo están implementadas "consultas" y
    /// "procedimientos". Faltan urgencias, hospitalizacion, recienNacidos, medicamentos y
    /// otrosServicios, cada una con sus propios campos de CIE-11.
    /// </remarks>
    public class MuvRipsRoot
    {
        [JsonPropertyName("numDocumentoIdObligado")]
        public string NumDocumentoIdObligado { get; set; } = string.Empty;

        [JsonPropertyName("numFactura")]
        public string? NumFactura { get; set; }

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

        // --- CIE-11 (Resolución 948 de 2026, campos C23 a C30) ---
        // Por cada diagnóstico en CIE-10 la norma pide dos campos más: el código en CIE-11 y su
        // nombre. Van en null mientras el prestador no los informe, pero la llave debe viajar.

        [JsonPropertyName("codDiagnosticoPrincipalCIE11")]
        public string? CodDiagnosticoPrincipalCIE11 { get; set; }

        [JsonPropertyName("nomCodDiagnosticoPrincipalCIE11")]
        public string? NomCodDiagnosticoPrincipalCIE11 { get; set; }

        [JsonPropertyName("codDiagnosticoRelacionado1CIE11")]
        public string? CodDiagnosticoRelacionado1CIE11 { get; set; }

        [JsonPropertyName("nomCodDiagnosticoRelacionado1CIE11")]
        public string? NomCodDiagnosticoRelacionado1CIE11 { get; set; }

        [JsonPropertyName("codDiagnosticoRelacionado2CIE11")]
        public string? CodDiagnosticoRelacionado2CIE11 { get; set; }

        [JsonPropertyName("nomCodDiagnosticoRelacionado2CIE11")]
        public string? NomCodDiagnosticoRelacionado2CIE11 { get; set; }

        [JsonPropertyName("codDiagnosticoRelacionado3CIE11")]
        public string? CodDiagnosticoRelacionado3CIE11 { get; set; }

        [JsonPropertyName("nomCodDiagnosticoRelacionado3CIE11")]
        public string? NomCodDiagnosticoRelacionado3CIE11 { get; set; }

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

        // --- CIE-11 (Resolución 948 de 2026, campos P21 a P26) ---
        // Ojo con la diferencia respecto a consultas: acá el diagnóstico relacionado es uno solo
        // (sin numerar) y además la complicación también lleva su par en CIE-11.

        [JsonPropertyName("codDiagnosticoPrincipalCIE11")]
        public string? CodDiagnosticoPrincipalCIE11 { get; set; }

        [JsonPropertyName("nomCodDiagnosticoPrincipalCIE11")]
        public string? NomCodDiagnosticoPrincipalCIE11 { get; set; }

        [JsonPropertyName("codDiagnosticoRelacionadoCIE11")]
        public string? CodDiagnosticoRelacionadoCIE11 { get; set; }

        [JsonPropertyName("nomCodDiagnosticoRelacionadoCIE11")]
        public string? NomCodDiagnosticoRelacionadoCIE11 { get; set; }

        [JsonPropertyName("codComplicacionCIE11")]
        public string? CodComplicacionCIE11 { get; set; }

        [JsonPropertyName("nomCodComplicacionCIE11")]
        public string? NomCodComplicacionCIE11 { get; set; }

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
