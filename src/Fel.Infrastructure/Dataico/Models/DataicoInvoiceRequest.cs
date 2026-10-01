using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Fel.Infrastructure.Dataico.Models
{
    public class DataicoInvoiceItem
    {
        public string sku { get; set; } = string.Empty;
        public decimal quantity { get; set; }
        public string description { get; set; } = string.Empty;

        [JsonPropertyName("measuring_unit")]
        public string measuring_unit { get; set; } = "94";

        public decimal price { get; set; }

        [JsonPropertyName("discount_rate")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public decimal discount_rate { get; set; }

        public List<DataicoTax> taxes { get; set; } = new();

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<DataicoTax>? retentions { get; set; }
    }

    public class DataicoInvoiceActions
    {
        [JsonPropertyName("send_dian")]
        public bool send_dian { get; set; } = true;

        [JsonPropertyName("send_email")]
        public bool send_email { get; set; } = false;
    }

    // Cuerpo real esperado por Dataico: POST /direct/dataico_api/v2/invoices
    public class DataicoInvoiceEnvelope
    {
        public DataicoInvoiceActions actions { get; set; } = new();
        public DataicoInvoiceRequest invoice { get; set; } = new();
    }

    public class DataicoInvoiceRequest
    {
        public string env { get; set; } = "PRUEBAS"; // PRUEBAS | PRODUCCION

        [JsonPropertyName("dataico_account_id")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? dataico_account_id { get; set; }

        public string number { get; set; } = string.Empty;

        [JsonPropertyName("issue_date")]
        public string issue_date { get; set; } = string.Empty; // dd/MM/yyyy

        [JsonPropertyName("payment_date")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? payment_date { get; set; }

        [JsonPropertyName("order_reference")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? order_reference { get; set; }

        [JsonPropertyName("invoice_type_code")]
        public string invoice_type_code { get; set; } = "FACTURA_VENTA";

        [JsonPropertyName("payment_means")]
        public string payment_means { get; set; } = string.Empty; // catálogo Dataico, ej. EFECTIVO, DEBIT_CARD, TRANSFERENCIA

        [JsonPropertyName("payment_means_type")]
        public string payment_means_type { get; set; } = string.Empty; // catálogo Dataico, ej. CREDITO, DEBITO

        public DataicoNumbering numbering { get; set; } = new();
        public DataicoParty customer { get; set; } = new();
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
