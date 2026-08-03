using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Fel.Api.Tenant.DTOs
{
    /// <summary>
    /// Estructura para emitir los RIPS (Registro Individual de Prestación de Servicios de Salud) de forma aislada
    /// para reportar al Ministerio de Salud, independiente de la Facturación Electrónica.
    /// </summary>
    public class RipsEmitRequest
    {
        /// <summary>
        /// Código del prestador de servicios de salud (Código REPS)
        /// </summary>
        /// <example>0500112345</example>
        [Required]
        public string ProviderCode { get; set; } = string.Empty;

        /// <summary>
        /// Datos del paciente atendido
        /// </summary>
        [Required]
        public PatientDto Patient { get; set; } = new PatientDto();

        /// <summary>
        /// Lista de consultas médicas realizadas
        /// </summary>
        public List<ConsultationDto> Consultations { get; set; } = new List<ConsultationDto>();

        /// <summary>
        /// Lista de procedimientos médicos realizados
        /// </summary>
        public List<ProcedureDto> Procedures { get; set; } = new List<ProcedureDto>();
    }

    public class PatientDto
    {
        /// <example>CC</example>
        [Required]
        public string IdentificationType { get; set; } = string.Empty;

        /// <example>1020304050</example>
        [Required]
        public string IdentificationNumber { get; set; } = string.Empty;

        /// <example>Juan Pérez</example>
        [Required]
        public string FullName { get; set; } = string.Empty;

        /// <summary>
        /// Sexo Biológico (M / F)
        /// </summary>
        /// <example>M</example>
        [Required]
        [RegularExpression("^[MF]$", ErrorMessage = "El sexo biológico debe ser M o F.")]
        public string BiologicalSex { get; set; } = string.Empty;

        /// <summary>
        /// Fecha de nacimiento
        /// </summary>
        /// <example>1990-05-14</example>
        public DateTime BirthDate { get; set; }
    }

    public class ConsultationDto
    {
        /// <summary>
        /// Código diagnóstico principal (CIE-10)
        /// </summary>
        /// <example>J00</example>
        [Required]
        public string MainDiagnosisCode { get; set; } = string.Empty;

        /// <summary>
        /// Finalidad de la consulta (Ej: 01 - Atención Integral)
        /// </summary>
        /// <example>01</example>
        public string PurposeCode { get; set; } = "01";

        /// <example>5000.00</example>
        public decimal CopayAmount { get; set; }
    }

    public class ProcedureDto
    {
        /// <summary>
        /// Código del procedimiento (CUPS)
        /// </summary>
        /// <example>890201</example>
        [Required]
        public string ProcedureCode { get; set; } = string.Empty;

        /// <example>01</example>
        public string PurposeCode { get; set; } = "01";
    }
}
