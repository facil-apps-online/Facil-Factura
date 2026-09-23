using System.Text.Json.Serialization;

namespace Fel.Infrastructure.Dataico.Models
{
    // Paso 2 del flujo de "documentos personalizados": tras el Paso 1 (enviar a la DIAN sin
    // correo), se reenvía el mismo documento con el PDF propio ya renderizado para que Dataico
    // solo despache el correo — confirmado contra una integración real ya en producción (proyecto
    // de migración en C:\FEL, método FacturacionService.EnviarPDFFacturas): PUT al mismo recurso
    // (invoices/credit_notes/debit_notes) + "/{cufe}".
    public class DataicoCustomDocumentActions
    {
        public bool send_dian { get; set; }
        public bool send_email { get; set; }
        public string? email { get; set; }
        public string? pdf { get; set; } // Base64 del PDF renderizado
    }

    public class DataicoCustomDocumentInvoiceRef
    {
        public string env { get; set; } = "PRUEBAS";

        [JsonPropertyName("dataico_account_id")]
        public string? dataico_account_id { get; set; }
    }

    public class DataicoCustomDocumentEnvelope
    {
        public DataicoCustomDocumentActions actions { get; set; } = new();
        public DataicoCustomDocumentInvoiceRef invoice { get; set; } = new();
    }
}
