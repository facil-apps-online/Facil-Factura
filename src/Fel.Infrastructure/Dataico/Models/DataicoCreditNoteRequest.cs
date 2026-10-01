using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Fel.Infrastructure.Dataico.Models
{
    // Cuerpo real esperado por Dataico: POST /direct/dataico_api/v2/credit_notes — confirmado
    // contra una integración real ya en producción (proyecto de migración en C:\FEL). A diferencia
    // de la factura, no lleva customer ni payment_means: hereda todo eso de la factura que
    // referencia mediante invoice_id.
    public class DataicoCreditNoteEnvelope
    {
        public DataicoInvoiceActions actions { get; set; } = new();

        [JsonPropertyName("credit_note")]
        public DataicoCreditNoteRequest credit_note { get; set; } = new();
    }

    public class DataicoCreditNoteRequest
    {
        public string env { get; set; } = "PRUEBAS"; // PRUEBAS | PRODUCCION

        [JsonPropertyName("dataico_account_id")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? dataico_account_id { get; set; }

        // Id interno que Dataico le asignó a la factura original (su "uuid" de respuesta) — no es
        // nuestro Id ni el CUFE.
        [JsonPropertyName("invoice_id")]
        public string invoice_id { get; set; } = string.Empty;

        [JsonPropertyName("issue_date")]
        public string issue_date { get; set; } = string.Empty; // dd/MM/yyyy

        // Catálogo cerrado de Dataico confirmado con ejemplos reales: DEVOLUCION, ANULACION, OTROS.
        public string reason { get; set; } = "OTROS";

        public string number { get; set; } = string.Empty;

        public DataicoNumbering numbering { get; set; } = new();
        public List<DataicoInvoiceItem> items { get; set; } = new();

        // Retenciones definidas una sola vez para todo el documento (ReteICA, ReteIVA, ReteFuente...),
        // solo con categoría y tarifa: Dataico calcula base y valor con su propio redondeo. Es la forma
        // de los ejemplos oficiales de su API (POST invoices y credit_notes con "retentions" al mismo
        // nivel que "items"). Las retenciones por ítem siguen viajando dentro de cada ítem.
        [JsonPropertyName("retentions")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<DataicoTax>? retentions { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<DataicoCharge>? charges { get; set; }
    }
}
