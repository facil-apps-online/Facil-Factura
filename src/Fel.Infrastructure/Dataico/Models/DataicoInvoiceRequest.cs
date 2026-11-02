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

        // Factura de mandato: Dataico recibe el mandante de cada línea y marca el documento con el tipo de operación "Mandatos".
        // Confirmado contra el ejemplo "Enviar factura - Mandato" del Postman de Dataico y contra la integración de escritorio (FEL).
        [JsonPropertyName("mandante_identification")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? mandante_identification { get; set; }

        [JsonPropertyName("mandante_identification_type")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? mandante_identification_type { get; set; }

        // Factura AIU: la línea del servicio contratado se marca con aiu_administration y apunta con aiu_contract a la línea de Utilidad
        // (SKU "AIU-U"). Ejemplos "Enviar factura - AIU" del Postman de Dataico.
        [JsonPropertyName("aiu_administration")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public bool? aiu_administration { get; set; }

        [JsonPropertyName("aiu_contract")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? aiu_contract { get; set; }
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

        // "MANDATO" para la factura de mandato (operación 11); sin valor en la factura estándar.
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? operation { get; set; }

        // Contrato de la factura AIU (operation "AIU"): la API genera las líneas de Administración, Imprevistos, Utilidad y servicio a partir de
        // esto; el integrador no envía items. Solo lo lee la API de integración, nunca viaja a Dataico con este nombre.
        [JsonPropertyName("aiu")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public DataicoAiuContract? aiu { get; set; }

        // Documento equivalente POS (operation "POS"): datos de la caja de venta y, si el comprador tiene programa de beneficios, los suyos.
        // Solo lo lee la API de integración; nunca viaja a Dataico.
        [JsonPropertyName("pos")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public DataicoPosData? pos { get; set; }

        // Observaciones del documento, una por entrada y en orden.
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<string>? notes { get; set; }

        // Factura en moneda extranjera: moneda ISO 4217, TRM (pesos por unidad) y su fecha dd/MM/yyyy. Los valores de los ítems van en
        // esa moneda. Sin valor (nulos) cuando la factura es en COP. Confirmado con el ejemplo de factura en USD de Dataico.
        [JsonPropertyName("currency")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? currency { get; set; }

        [JsonPropertyName("currency_exchange_rate")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public decimal? currency_exchange_rate { get; set; }

        [JsonPropertyName("currency_exchange_rate_date")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? currency_exchange_rate_date { get; set; }

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

    // Datos de la caja de venta del documento equivalente POS (Anexo Técnico, numeral 8.2.1.2). Los beneficios del comprador son opcionales pero van completos.
    public class DataicoPosData
    {
        public string box_plate { get; set; } = string.Empty;
        public string box_location { get; set; } = string.Empty;
        public string cashier { get; set; } = string.Empty;
        public string box_type { get; set; } = string.Empty;
        public string sale_code { get; set; } = string.Empty;
        public string? buyer_code { get; set; }
        public string? buyer_name { get; set; }
        public decimal? buyer_points { get; set; }
    }

    // Datos del contrato AIU que envía el integrador. Los porcentajes se aplican sobre base_amount; A e I no causan IVA, la Utilidad usa
    // utility_iva_rate (19 por defecto) y el servicio service_iva_rate (0 por defecto).
    public class DataicoAiuContract
    {
        public decimal base_amount { get; set; }
        public decimal? administration_percent { get; set; }
        public decimal? unforeseen_percent { get; set; }
        public decimal? utility_percent { get; set; }
        public decimal? utility_iva_rate { get; set; }
        public decimal? service_iva_rate { get; set; }
        public string contract_object { get; set; } = string.Empty;
    }
}
