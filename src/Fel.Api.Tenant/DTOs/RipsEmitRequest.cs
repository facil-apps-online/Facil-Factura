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

        /// <summary>
        /// Tipo de usuario según el régimen de afiliación (01 Contributivo cotizante,
        /// 02 Contributivo beneficiario, 03 Subsidiado, 04 Vinculado, etc.).
        /// </summary>
        /// <example>01</example>
        [Required(ErrorMessage = "El tipo de usuario (régimen) del paciente es obligatorio: antes se reportaba siempre como contributivo.")]
        public string UserType { get; set; } = string.Empty;

        /// <summary>
        /// Código DANE del municipio de residencia del paciente.
        /// </summary>
        /// <example>05001</example>
        [Required(ErrorMessage = "El municipio de residencia del paciente es obligatorio: antes se reportaba siempre Bogotá.")]
        public string ResidenceMunicipalityCode { get; set; } = string.Empty;

        /// <summary>
        /// Zona territorial de residencia: 01 Urbana, 02 Rural.
        /// </summary>
        /// <example>01</example>
        [Required(ErrorMessage = "La zona territorial de residencia es obligatoria: antes se reportaba siempre urbana.")]
        public string ResidenceZoneCode { get; set; } = string.Empty;

        /// <summary>
        /// Código del país de residencia. Colombia (170) por defecto, que es el único caso con un
        /// valor por defecto honesto: si el paciente reside fuera hay que informarlo.
        /// </summary>
        /// <example>170</example>
        public string ResidenceCountryCode { get; set; } = "170";
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
        /// Código diagnóstico principal en CIE-11. Opcional: la Resolución 948 de 2026 lo agregó
        /// junto al de CIE-10, no en reemplazo. Si no se informa viaja como null.
        /// </summary>
        /// <example>CA00</example>
        public string? MainDiagnosisCodeCie11 { get; set; }

        /// <summary>
        /// Nombre del diagnóstico principal según CIE-11. Acompaña a
        /// <see cref="MainDiagnosisCodeCie11"/>; la norma pide el código y su descripción.
        /// </summary>
        /// <example>Rinofaringitis aguda</example>
        public string? MainDiagnosisNameCie11 { get; set; }

        /// <summary>
        /// Finalidad de la consulta (Ej: 01 - Atención Integral)
        /// </summary>
        /// <example>01</example>
        public string PurposeCode { get; set; } = "01";

        /// <summary>
        /// Fecha y hora real en que se prestó la atención. Antes se enviaba la fecha del envío,
        /// que no es lo mismo y falsea el reporte.
        /// </summary>
        /// <example>2026-09-23T08:30:00</example>
        [Required(ErrorMessage = "La fecha de la atención es obligatoria.")]
        // Anulable a propósito: [Required] no valida nada sobre un DateTime no anulable, porque
        // nunca es null — si el llamador lo omite queda en 0001-01-01 y pasa la validación.
        public DateTime? ServiceDate { get; set; }

        /// <summary>
        /// Código CUPS de la consulta prestada.
        /// </summary>
        /// <example>890201</example>
        [Required(ErrorMessage = "El código CUPS de la consulta es obligatorio: antes se enviaba siempre 890201.")]
        public string ConsultationCode { get; set; } = string.Empty;

        /// <summary>
        /// Causa o motivo de la atención (13 Enfermedad general, 15 Accidente de trabajo, etc.).
        /// </summary>
        /// <example>13</example>
        [Required(ErrorMessage = "La causa o motivo de la atención es obligatoria: antes se enviaba siempre enfermedad general.")]
        public string AttentionCauseCode { get; set; } = string.Empty;

        /// <summary>
        /// Tipo de diagnóstico principal (01 Impresión diagnóstica, 02 Confirmado nuevo,
        /// 03 Confirmado repetido).
        /// </summary>
        /// <example>01</example>
        [Required(ErrorMessage = "El tipo de diagnóstico principal es obligatorio.")]
        public string MainDiagnosisType { get; set; } = string.Empty;

        /// <summary>
        /// Modalidad del grupo de servicio (01 Intramural, 02 Extramural, 03 Telemedicina).
        /// </summary>
        /// <example>01</example>
        [Required(ErrorMessage = "La modalidad del grupo de servicio es obligatoria.")]
        public string ServiceModalityCode { get; set; } = string.Empty;

        /// <summary>
        /// Grupo de servicios habilitado del prestador (01 Consulta externa, 02 Apoyo diagnóstico,
        /// 03 Internación, etc.).
        /// </summary>
        /// <example>01</example>
        [Required(ErrorMessage = "El grupo de servicios es obligatorio.")]
        public string ServiceGroupCode { get; set; } = string.Empty;

        /// <summary>
        /// Código del servicio habilitado en el REPS del prestador.
        /// </summary>
        /// <example>334</example>
        [Required(ErrorMessage = "El código del servicio es obligatorio.")]
        // Anulable por la misma razón que ServiceDate: sobre un int no anulable, [Required] deja
        // pasar el cero.
        public int? ServiceCode { get; set; }

        /// <summary>
        /// Valor total del servicio prestado. No es el copago: antes se enviaba el copago en su
        /// lugar, lo que reportaba mal el valor de la atención.
        /// </summary>
        /// <example>45000.00</example>
        [Required(ErrorMessage = "El valor del servicio es obligatorio.")]
        // Anulable para que [Required] distinga "no lo informaron" de "vale cero", que en un
        // servicio de salud son cosas distintas.
        public decimal? ServiceValue { get; set; }

        /// <summary>
        /// Concepto del recaudo (01 Copago, 02 Cuota moderadora, 05 No aplica, etc.).
        /// </summary>
        /// <example>01</example>
        [Required(ErrorMessage = "El concepto de recaudo es obligatorio.")]
        public string CollectionConceptCode { get; set; } = string.Empty;

        /// <summary>
        /// Número de la factura del pago moderador, cuando aplique.
        /// </summary>
        public string? ModeratorPaymentInvoiceNumber { get; set; }

        /// <summary>
        /// Número de autorización de la consulta, cuando aplique.
        /// </summary>
        public string? AuthorizationNumber { get; set; }

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

        /// <summary>
        /// Código diagnóstico principal en CIE-11 asociado al procedimiento. Opcional, agregado
        /// por la Resolución 948 de 2026.
        /// </summary>
        /// <example>CA00</example>
        public string? MainDiagnosisCodeCie11 { get; set; }

        /// <summary>
        /// Nombre del diagnóstico principal según CIE-11.
        /// </summary>
        /// <example>Rinofaringitis aguda</example>
        public string? MainDiagnosisNameCie11 { get; set; }

        /// <example>01</example>
        public string PurposeCode { get; set; } = "01";

        /// <summary>
        /// Código diagnóstico principal (CIE-10) que motivó el procedimiento. Antes se enviaba
        /// siempre Z000, que es un diagnóstico real y por eso el error pasaba desapercibido.
        /// </summary>
        /// <example>J00</example>
        [Required(ErrorMessage = "El diagnóstico principal del procedimiento es obligatorio: antes se enviaba siempre Z000.")]
        public string MainDiagnosisCode { get; set; } = string.Empty;

        /// <summary>
        /// Fecha y hora real en que se realizó el procedimiento.
        /// </summary>
        /// <example>2026-09-23T09:15:00</example>
        [Required(ErrorMessage = "La fecha del procedimiento es obligatoria.")]
        // Anulable a propósito: [Required] no valida nada sobre un DateTime no anulable, porque
        // nunca es null — si el llamador lo omite queda en 0001-01-01 y pasa la validación.
        public DateTime? ServiceDate { get; set; }

        /// <summary>
        /// Vía de ingreso al servicio de salud (01 Remitido, 02 Urgencias, 03 Consulta externa…).
        /// </summary>
        /// <example>03</example>
        [Required(ErrorMessage = "La vía de ingreso al servicio de salud es obligatoria.")]
        public string EntryRouteCode { get; set; } = string.Empty;

        /// <summary>
        /// Modalidad del grupo de servicio (01 Intramural, 02 Extramural, 03 Telemedicina).
        /// </summary>
        /// <example>01</example>
        [Required(ErrorMessage = "La modalidad del grupo de servicio es obligatoria.")]
        public string ServiceModalityCode { get; set; } = string.Empty;

        /// <summary>
        /// Grupo de servicios habilitado del prestador.
        /// </summary>
        /// <example>02</example>
        [Required(ErrorMessage = "El grupo de servicios es obligatorio.")]
        public string ServiceGroupCode { get; set; } = string.Empty;

        /// <summary>
        /// Código del servicio habilitado en el REPS del prestador.
        /// </summary>
        /// <example>706</example>
        [Required(ErrorMessage = "El código del servicio es obligatorio.")]
        // Anulable por la misma razón que ServiceDate: sobre un int no anulable, [Required] deja
        // pasar el cero.
        public int? ServiceCode { get; set; }

        /// <summary>
        /// Valor total del procedimiento. Antes se enviaba siempre cero.
        /// </summary>
        /// <example>120000.00</example>
        [Required(ErrorMessage = "El valor del procedimiento es obligatorio.")]
        // Anulable para que [Required] distinga "no lo informaron" de "vale cero", que en un
        // servicio de salud son cosas distintas.
        public decimal? ServiceValue { get; set; }

        /// <summary>
        /// Concepto del recaudo (01 Copago, 02 Cuota moderadora, 05 No aplica, etc.).
        /// </summary>
        /// <example>05</example>
        [Required(ErrorMessage = "El concepto de recaudo es obligatorio.")]
        public string CollectionConceptCode { get; set; } = string.Empty;

        /// <example>0.00</example>
        public decimal CopayAmount { get; set; }

        /// <summary>
        /// Código de complicación (CIE-10), cuando la hubo.
        /// </summary>
        public string? ComplicationCode { get; set; }

        /// <summary>
        /// Número de autorización del procedimiento, cuando aplique.
        /// </summary>
        public string? AuthorizationNumber { get; set; }

        /// <summary>
        /// Identificador MIPRES, cuando aplique.
        /// </summary>
        public string? MipresId { get; set; }
    }
}
