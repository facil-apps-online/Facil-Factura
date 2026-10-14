namespace Fel.Infrastructure.Dataico.Models
{
    // Evento de recepción (RADIAN) que Dataico firma y envía a la DIAN en nombre del Client: acuse de recibo,
    // reclamo, recibo del bien o aceptación expresa de una factura que le emitió un proveedor. Estructura
    // tomada de la colección de Dataico y de lo que ya envía en producción el legacy FEL (RecepcionService).
    public class DataicoReceptionEventEnvelope
    {
        public DataicoReceptionEvent reception_event { get; set; } = new();
    }

    public class DataicoReceptionEvent
    {
        public string env { get; set; } = "PRUEBAS";
        public string event_type { get; set; } = string.Empty; // ACUSE_DE_RECIBO | RECLAMO | RECIBO_DE_PRESTACION | ACEPTACION_EXPRESA
        public string document_type { get; set; } = "FACTURA_VENTA";
        public string document_number { get; set; } = string.Empty;
        public string document_cufe { get; set; } = string.Empty;
        public string issue_date { get; set; } = string.Empty; // dd/MM/yyyy
        public bool send_dian { get; set; } = true;
        public bool send_email { get; set; } = true;
        public DataicoEventPerson person { get; set; } = new();
        public string number { get; set; } = string.Empty;
        public DataicoEventNumbering numbering { get; set; } = new();
        public DataicoEventReceiver receiver { get; set; } = new();
    }

    // Quien realiza el evento: el Client que recibió la factura.
    public class DataicoEventPerson
    {
        public string first_name { get; set; } = string.Empty;
        public string family_name { get; set; } = string.Empty;
        public string party_identification { get; set; } = string.Empty;
        public string party_identification_type { get; set; } = "NIT";
        public string email { get; set; } = string.Empty;
        public string phone { get; set; } = string.Empty;
    }

    public class DataicoEventNumbering
    {
        public bool flexible { get; set; } = false;
    }

    // El proveedor: quien emitió la factura que se está acusando.
    public class DataicoEventReceiver
    {
        public string party_identification_type { get; set; } = "NIT";
        public string party_identification { get; set; } = string.Empty;
        public string party_type { get; set; } = "PERSONA_JURIDICA";
        public string tax_level_code { get; set; } = "RESPONSABLE_DE_IVA";
        public string regimen { get; set; } = "ORDINARIO";
        public string company_name { get; set; } = string.Empty;
        public string first_name { get; set; } = string.Empty;
        public string family_name { get; set; } = string.Empty;
        public string address_line { get; set; } = string.Empty;
        public string country_code { get; set; } = "CO";
        public string email { get; set; } = string.Empty;
        public string phone { get; set; } = string.Empty;
        public string city { get; set; } = string.Empty;
        public string department { get; set; } = string.Empty;
    }
}
