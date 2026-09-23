using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Fel.Infrastructure.Dataico.Models
{
    // Cuerpo esperado por Dataico: POST /direct/dataico_api/v2/debit_notes — la URL está confirmada
    // contra una integración real ya en producción (App.config del proyecto de migración en
    // C:\FEL), pero a diferencia de la Nota Crédito no se encontró ahí un ejemplo real del cuerpo
    // exacto. Se construyó por simetría con DataicoCreditNoteRequest — a verificar contra la
    // documentación/soporte de Dataico antes de depender de esto en producción.
    public class DataicoDebitNoteEnvelope
    {
        public DataicoInvoiceActions actions { get; set; } = new();

        [JsonPropertyName("debit_note")]
        public DataicoDebitNoteRequest debit_note { get; set; } = new();
    }

    public class DataicoDebitNoteRequest
    {
        public string env { get; set; } = "PRUEBAS"; // PRUEBAS | PRODUCCION

        [JsonPropertyName("dataico_account_id")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? dataico_account_id { get; set; }

        [JsonPropertyName("invoice_id")]
        public string invoice_id { get; set; } = string.Empty;

        [JsonPropertyName("issue_date")]
        public string issue_date { get; set; } = string.Empty; // dd/MM/yyyy

        public string reason { get; set; } = "OTROS";

        public string number { get; set; } = string.Empty;

        public DataicoNumbering numbering { get; set; } = new();
        public List<DataicoInvoiceItem> items { get; set; } = new();

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<DataicoCharge>? charges { get; set; }
    }
}
