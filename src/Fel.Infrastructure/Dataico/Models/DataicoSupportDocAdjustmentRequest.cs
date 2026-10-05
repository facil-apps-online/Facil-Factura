using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Fel.Infrastructure.Dataico.Models
{
    // Cuerpo real esperado por Dataico: POST /direct/dataico_api/v2/support_doc_adjustments —
    // confirmado contra una integración real ya en producción (proyecto de migración en C:\FEL).
    // A diferencia de factura/notas, send_dian/send_email van DENTRO del objeto principal, no en
    // un "actions" aparte, y sí repite los datos completos del proveedor (no hereda de un id).
    public class DataicoSupportDocAdjustmentEnvelope
    {
        [JsonPropertyName("support_doc_adjustment")]
        public DataicoSupportDocAdjustmentRequest support_doc_adjustment { get; set; } = new();
    }

    public class DataicoSupportDocAdjustmentRequest
    {
        public string env { get; set; } = "PRUEBAS";

        [JsonPropertyName("send_dian")]
        public bool send_dian { get; set; } = true;

        [JsonPropertyName("send_email")]
        public bool send_email { get; set; } = false;

        public string number { get; set; } = string.Empty;

        [JsonPropertyName("issue_date")]
        public string issue_date { get; set; } = string.Empty; // dd/MM/yyyy

        [JsonPropertyName("payment_date")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? payment_date { get; set; }

        // Solo se confirmó "RESIDENTE" contra un ejemplo real; se asume por defecto ya que no hay
        // un campo equivalente en nuestro modelo de terceros para derivarlo.
        [JsonPropertyName("resident_type")]
        public string resident_type { get; set; } = "RESIDENTE";

        [JsonPropertyName("payment_means")]
        public string payment_means { get; set; } = string.Empty;

        [JsonPropertyName("payment_means_type")]
        public string payment_means_type { get; set; } = string.Empty;

        // Igual que las notas crédito y débito: solo prefijo y flexible, SIN número de resolución. Verificado
        // contra Dataico (octubre 2026): con resolution_number responde "No se encuentra numeración" y sin
        // prefijo responde "Esta cuenta tiene más de una numeración ... se debe especificar el prefix".
        public DataicoNumbering numbering { get; set; } = new();

        // Solo "AJUSTE_PRECIO" está confirmado contra un ejemplo real — el catálogo completo de
        // Dataico para esta nota no se pudo verificar, así que por ahora queda fijo.
        [JsonPropertyName("discrepancy_code")]
        public string discrepancy_code { get; set; } = "AJUSTE_PRECIO";

        [JsonPropertyName("discrepancy_description")]
        public string discrepancy_description { get; set; } = string.Empty;

        [JsonPropertyName("source_document_cuds")]
        public string source_document_cuds { get; set; } = string.Empty;

        [JsonPropertyName("source_document_issue_date")]
        public string source_document_issue_date { get; set; } = string.Empty;

        [JsonPropertyName("source_document_number")]
        public string source_document_number { get; set; } = string.Empty;

        public DataicoParty customer { get; set; } = new();
        public List<DataicoInvoiceItem> items { get; set; } = new();

        // Descuento/cargo generales y ReteICA, igual que en el documento soporte al que ajusta.
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<DataicoCharge>? charges { get; set; }
    }
}
