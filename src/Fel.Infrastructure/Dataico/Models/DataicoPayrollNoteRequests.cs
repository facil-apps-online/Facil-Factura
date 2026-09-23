using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Fel.Infrastructure.Dataico.Models
{
    // Referencia al comprobante de nómina original por su CUNE (equivalente al CUFE de nómina) —
    // confirmado contra una integración real ya en producción (proyecto de migración en C:\FEL).
    public class DataicoPayrollDocumentReference
    {
        public string prefix { get; set; } = string.Empty;
        public long number { get; set; }
        public string cune { get; set; } = string.Empty;

        [JsonPropertyName("issue-date")]
        public string issue_date { get; set; } = string.Empty;
    }

    public class DataicoPayrollNote
    {
        public string text { get; set; } = string.Empty;
    }

    // Cuerpo real: POST /direct/payroll-api/v2/payroll-deletions (objeto plano, sin envelope). Solo
    // anula el comprobante original — no reenvía datos de nómina.
    public class DataicoPayrollDeletionRequest
    {
        public string env { get; set; } = "PRUEBAS";
        public string prefix { get; set; } = string.Empty;
        public long number { get; set; }

        [JsonPropertyName("issue-date")]
        public string issue_date { get; set; } = string.Empty;

        public DataicoPayrollDocumentReference entry { get; set; } = new();
        public List<DataicoPayrollNote> notes { get; set; } = new();
        public DataicoPayrollSoftware software { get; set; } = new();
    }

    // Cuerpo real: POST /direct/payroll-api/v2/payroll-replacements — mismo payload que un
    // comprobante de nómina normal (DataicoPayrollRequest), más la referencia a lo que reemplaza.
    public class DataicoPayrollReplacementRequest : DataicoPayrollRequest
    {
        public List<DataicoPayrollNote> notes { get; set; } = new();

        [JsonPropertyName("replacement-for")]
        public DataicoPayrollDocumentReference replacement_for { get; set; } = new();
    }
}
