using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Fel.Infrastructure.Dataico.Models
{
    public class DataicoParty
    {
        public string? email { get; set; }
        public string? phone { get; set; }

        [JsonPropertyName("party_identification_type")]
        public string party_identification_type { get; set; } = string.Empty;

        [JsonPropertyName("party_identification")]
        public string party_identification { get; set; } = string.Empty;

        [JsonPropertyName("party_type")]
        public string party_type { get; set; } = string.Empty; // "1" juridica, "2" natural

        [JsonPropertyName("tax_level_code")]
        public string? tax_level_code { get; set; }

        public string? regimen { get; set; }
        public string? department { get; set; }
        public string? city { get; set; }

        [JsonPropertyName("address_line")]
        public string? address_line { get; set; }

        [JsonPropertyName("country_code")]
        public string country_code { get; set; } = "CO";

        [JsonPropertyName("company_name")]
        public string? company_name { get; set; }

        [JsonPropertyName("first_name")]
        public string? first_name { get; set; }

        [JsonPropertyName("family_name")]
        public string? family_name { get; set; }
    }

    public class DataicoTax
    {
        [JsonPropertyName("tax_category")]
        public string tax_category { get; set; } = "IVA";

        [JsonPropertyName("tax_rate")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public decimal? tax_rate { get; set; }

        [JsonPropertyName("tax_amount")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public decimal? tax_amount { get; set; }

        [JsonPropertyName("base_amount")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public decimal? base_amount { get; set; }
    }

    // Descuento general del documento (ej. "pronto pago"), independiente del descuento por ítem.
    public class DataicoCharge
    {
        public string reason { get; set; } = string.Empty;

        [JsonPropertyName("base_amount")]
        public decimal base_amount { get; set; }

        public bool discount { get; set; } = true;
    }

    public class DataicoNumbering
    {
        [JsonPropertyName("resolution_number")]
        public string? resolution_number { get; set; }

        public string prefix { get; set; } = string.Empty;
        public bool flexible { get; set; } = true;
    }

    public class DataicoResult
    {
        public bool Success { get; set; }
        public int StatusCode { get; set; }
        public string RawResponse { get; set; } = string.Empty;
        public string? DocumentNumber { get; set; }
        public string? Cufe { get; set; }
        // Id interno que Dataico asigna a este documento (campo "uuid" de su respuesta) — lo
        // necesita cualquier Nota Crédito/Débito que lo referencie más adelante.
        public string? DataicoDocumentId { get; set; }
        public string? XmlUrl { get; set; }
        public string? PdfUrl { get; set; }
        public string? QrCode { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
