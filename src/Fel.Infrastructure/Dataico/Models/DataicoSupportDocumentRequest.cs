using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Fel.Infrastructure.Dataico.Models
{
    public class DataicoSupportDocItem
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

    // Cuerpo real esperado por Dataico: POST /direct/dataico_api/v2/support_docs
    public class DataicoSupportDocumentEnvelope
    {
        public DataicoSupportDocumentRequest support_doc { get; set; } = new();
    }

    public class DataicoSupportDocumentRequest
    {
        public string env { get; set; } = "PRUEBAS";

        [JsonPropertyName("send_dian")]
        public bool send_dian { get; set; } = true;

        [JsonPropertyName("send_email")]
        public bool send_email { get; set; } = false;

        public string number { get; set; } = string.Empty;

        [JsonPropertyName("issue_date")]
        public string issue_date { get; set; } = string.Empty;

        [JsonPropertyName("payment_date")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? payment_date { get; set; }

        [JsonPropertyName("order_reference")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? order_reference { get; set; }

        [JsonPropertyName("resident_type")]
        public string resident_type { get; set; } = "RESIDENTE";

        [JsonPropertyName("generation_type")]
        public string generation_type { get; set; } = "POR_OPERACION";

        [JsonPropertyName("payment_means")]
        public string payment_means { get; set; } = string.Empty;

        [JsonPropertyName("payment_means_type")]
        public string payment_means_type { get; set; } = string.Empty;

        public string currency { get; set; } = "COP";

        public DataicoNumbering numbering { get; set; } = new();
        public DataicoParty customer { get; set; } = new(); // El proveedor/tercero al que se le emite el documento soporte
        public List<DataicoSupportDocItem> items { get; set; } = new();

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<DataicoCharge>? charges { get; set; }
    }
}
