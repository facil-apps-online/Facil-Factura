using System;
using System.Collections.Generic;

namespace Fel.Core.Entities
{
    public class Client
    {
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }
        public Tenant Tenant { get; set; } = null!;
        
        public string CompanyName { get; set; } = string.Empty;
        public string PersonType { get; set; } = "PJ"; // PN = Persona Natural, PJ = Persona Jurídica
        public string CommercialName { get; set; } = string.Empty;
        public string TaxId { get; set; } = string.Empty; // NIT
        public string VerificationDigit { get; set; } = string.Empty; // DV

        // Asociado/comercial del Tenant a cargo de este cliente (opcional).
        public Guid? AssociateId { get; set; }
        public Associate? Associate { get; set; }

        // Ubicación y Contacto
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string? CityCode { get; set; } // Código DANE del municipio (5 dígitos), para emitir directo a la DIAN
        public string? OrganizationDepartment { get; set; } // Departamento de la organización, para el certificado
        public string? OrganizationType { get; set; } // Código del tipo de organización requerido por Viafirma

        // Representante legal registrado en la hoja de representación del RUT (casillas 98-110).
        // Es opcional: el RUT puede venir solo con la hoja principal y algunas entidades pueden
        // requerir que el usuario complete o corrija estos datos manualmente.
        public string? LegalRepresentativeFirstName { get; set; }
        public string? LegalRepresentativeOtherNames { get; set; }
        public string? LegalRepresentativeFirstLastName { get; set; }
        public string? LegalRepresentativeSecondLastName { get; set; }
        public string? LegalRepresentativeDocumentType { get; set; }
        public string? LegalRepresentativeDocumentNumber { get; set; }
        public string? LegalRepresentativeDocumentCountryCode { get; set; }
        public string? LegalRepresentativeEmail { get; set; }
        public string? LegalRepresentativeRepresentationCode { get; set; }
        public DateTime? LegalRepresentativeStartDate { get; set; }

        // Área/departamento donde trabaja el representante legal dentro de la empresa (ej. "Gerencia
        // General", "Legal") — la exige Viafirma como Unidad Organizacional del certificado. No
        // confundir con OrganizationDepartment, que es el departamento geográfico (Google Maps/RUT).
        public string? LegalRepresentativeOrganizationalArea { get; set; }
        
        // Fiscal
        public string TaxRegime { get; set; } = string.Empty;
        public string EconomicActivity { get; set; } = string.Empty;

        // Responsabilidades del RUT (casilla 53) que la representación gráfica debe declarar
        // explícitamente además del régimen de IVA — 13 = Gran Contribuyente, 09 = Agente
        // Retenedor de IVA, 15 = Autorretenedor de renta.
        public bool IsGranContribuyente { get; set; }
        public bool IsAgenteRetenedorIva { get; set; }
        public bool IsAutorretenedorRenta { get; set; }

        // null = usar el DisplayFormat que trae cada fila del catálogo de Unidades de Medida;
        // con valor, el Client fuerza el mismo formato para todas sus unidades en sus facturas
        // (Combined = "94 - EA", CodeOnly = "94", AbbreviationOnly = "EA").
        public string? UnitOfMeasureDisplayOverride { get; set; }

        // Formato numérico de este cliente, tanto en el portal de clientes (campos y totales) como en
        // sus PDF: "." = punto decimal y coma de miles (1,234,567.89 — el estándar acá y el valor por
        // defecto para los clientes existentes); "," = coma decimal y punto de miles (1.234.567,89).
        // Es solo de presentación: los valores siempre viajan al servidor como números con punto.
        public string DecimalSeparator { get; set; } = ".";

        // Ya no tiene ningún efecto: la retención se elige 100% manual por línea de factura, sin
        // ninguna resolución automática que este campo pudiera activar/desactivar. Se conserva sin
        // usar por si se retoma la automatización más adelante.
        public bool AppliesRetentions { get; set; } = true;

        // Georeferenciación
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }

        // --- DIAN Habilitation & Software Propio ---
        public string SoftwareId { get; set; } = string.Empty;
        public string SoftwarePin { get; set; } = string.Empty;
        public string TestSetId { get; set; } = string.Empty;
        public string DianHabilitationStatus { get; set; } = "Pending"; // Pending, InProgress, Passed, Production
        public int DianHabilitationProgress { get; set; } = 0;
        public string DianHabilitationMessage { get; set; } = string.Empty;

        // Consecutivo anual del nombre de archivo exigido por el anexo técnico (numeral 6.5.7/6.5.8) —
        // el "dddddddd" hexadecimal que identifica cada XML/ZIP enviado a la DIAN, independiente del
        // consecutivo de numeración de la propia factura. Se reinicia a 1 cada 1 de enero.
        public int DianFileSequence { get; set; } = 0;
        public int DianFileSequenceYear { get; set; } = 0;

        // Cantidades reales que la DIAN exige para este TestSetId, leídas de /TestSet/View al
        // registrar el software propio (ver DianHabilitationScraperService) — nunca un número fijo
        // en el código, porque cada contribuyente puede tener topes distintos. "Required" es el
        // máximo de intentos permitidos por tipo; "RequiredAccepted" es cuántos de esos deben quedar
        // con estado aceptado para que la DIAN dé por superado el set de pruebas.
        public int TestSetRequiredInvoices { get; set; }
        public int TestSetRequiredCreditNotes { get; set; }
        public int TestSetRequiredDebitNotes { get; set; }
        public int TestSetRequiredAcceptedInvoices { get; set; }
        public int TestSetRequiredAcceptedCreditNotes { get; set; }
        public int TestSetRequiredAcceptedDebitNotes { get; set; }

        // Cuántos documentos de prueba de cada tipo ya se enviaron a la DIAN — gobierna qué tipo
        // envía DianTestSetSubmissionService.SendNextTestDocumentAsync en la próxima llamada.
        public int TestSetSentInvoices { get; set; }
        public int TestSetSentCreditNotes { get; set; }
        public int TestSetSentDebitNotes { get; set; }

        // CUFE + número de la última factura de prueba enviada, para poder referenciarla al armar
        // una Nota Crédito/Débito de prueba (la DIAN exige que la nota referencie una factura real
        // dentro de InvoiceDocumentReference).
        public string? TestSetLastInvoiceCufe { get; set; }
        public string? TestSetLastInvoiceNumber { get; set; }

        // --- Sector Salud: credenciales del profesional/prestador ante el MUV-FEV-RIPS (SIIFA) ---
        // Cada prestador hace su propia autenticación (LoginSISPRO) con sus propias credenciales,
        // igual que con el certificado de la DIAN — FacilFactura solo opera la integración a su
        // nombre. Sin UI en los portales todavía (pendiente).
        // Tipo de DOCUMENTO de quien autentica (CC, CE, ...). Ojo: hasta la Res 948 este campo
        // recibía indistintamente CC, RE, PIN, PINx y PIE, mezclando dos cosas que el manual
        // FEV-RIPS trata por separado — el tipo de documento va en persona.identificacion.tipo y
        // el tipo de usuario en tipoUsuario. Enviar "RE" como tipo de documento hacía fallar el
        // LoginSISPRO de cualquier prestador que no fuera persona natural con cédula.
        public string? MinSaludIdentificationType { get; set; }
        public string? MinSaludIdentificationNumber { get; set; }
        public string? MinSaludPasswordEncrypted { get; set; }

        /// <summary>
        /// Campo "tipoUsuario" del LoginSISPRO: RE (Representante Entidad), PIN (Profesional
        /// Independiente Nacional), PINx (de Excepción) o PIE (Extranjero). Es opcional según el
        /// manual; para PSS/PTS debe informarse RE.
        /// </summary>
        public string? MinSaludUserType { get; set; }

        // --- Entorno del MUV ---
        // El Ministerio publica dos ambientes con imágenes y hosts distintos: producción
        // (fevrips.sispro.gov.co) y pruebas (stage-fevrips.sispropreprod.gov.co). Cada uno exige
        // sus propias credenciales, igual que TestApiKey/LiveApiKey del lado DIAN, así que no se
        // pueden guardar en los mismos campos sin obligar al cliente a reescribirlas cada vez que
        // cambia de ambiente.
        public string MinSaludEnvironment { get; set; } = MinSaludEnvironments.Test;

        public string? MinSaludTestIdentificationType { get; set; }
        public string? MinSaludTestIdentificationNumber { get; set; }
        public string? MinSaludTestPasswordEncrypted { get; set; }

        // La URL del contenedor FEV-RIPS NO vive acá ni en el Tenant: es configuración de la
        // plataforma ("MinSalud:MuvProductionUrl" y "MinSalud:MuvTestUrl"), porque la instalación
        // del MUV es una sola y es nuestra. Antes era un campo por Client, lo que obligaba a
        // repetir el mismo valor en cada uno y convertía cada repetición en una oportunidad de
        // escribirlo mal — y una URL mal escrita falla en silencio. Ningún Client llegó a usarlo.

        // --- IHCE (Interoperabilidad de Historia Clínica Electrónica) ---
        // Credenciales por Client, no por Tenant: según MinSalud, el proveedor tecnológico no se
        // registra de forma independiente en IHCE — es cada IPS/profesional independiente (nuestro
        // Client) quien tramita sus propias llaves en el Portal de Administración de Llaves
        // (Mi Seguridad Social -> Hércules -> ihcecol.sispro.gov.co) y nos las entrega para
        // configurar/probar la integración en su nombre. Mismo patrón que SoftwareId/SoftwarePin
        // de la DIAN o las credenciales de MinSalud arriba.
        public string? IhceClientId { get; set; }
        public string? IhceClientSecretEncrypted { get; set; }
        public string? IhceApimSubscriptionKey { get; set; }
        public string? IhceTenantId { get; set; }
        public string? IhceEndpoint { get; set; }
        public string IhceEnvironment { get; set; } = "Sandbox"; // Sandbox (Preproducción) o Production

        // --- API Integration (HMAC) ---
        public string LiveApiKey { get; set; } = Guid.NewGuid().ToString("N");
        public string LiveApiSecret { get; set; } = Guid.NewGuid().ToString("N");
        public string TestApiKey { get; set; } = "test_" + Guid.NewGuid().ToString("N");
        public string TestApiSecret { get; set; } = Guid.NewGuid().ToString("N");
        
        // --- Billing ---
        public decimal PricePerDocument { get; set; } = 0m; // Default price set by Tenant for this Client

        // Tarifa comercial del Tenant hacia este Client, independiente de PricePerDocument
        // y del modelo de facturación de Superadmin hacia el Tenant.
        public decimal SubscriptionRate { get; set; } = 0m;
        public BillingFrequency BillingFrequency { get; set; } = BillingFrequency.Monthly;

        // --- Proveedor de Documentos Electrónicos ---
        public Guid IntegratorId { get; set; }
        public Integrator Integrator { get; set; } = null!;
        public string DataicoApiUser { get; set; } = string.Empty;
        public string DataicoApiPasswordEncrypted { get; set; } = string.Empty;
        public string DataicoAuthTokenEncrypted { get; set; } = string.Empty;
        public string DataicoAccountId { get; set; } = string.Empty;
        public string DataicoEnvironment { get; set; } = "PRUEBAS"; // PRUEBAS o PRODUCCION
        
        // --- Branding (White Label, con fallback al branding del Tenant) ---
        public string LogoLightUrl { get; set; } = string.Empty;
        public string LogoDarkUrl { get; set; } = string.Empty;
        public string PrimaryColorLight { get; set; } = "#2563eb"; // Blue-600 default
        public string PrimaryColorDark { get; set; } = "#f8fafc";  // Slate-50 default

        // --- SMTP propio (opcional, para reenvío de documentos del flujo nativo DIAN) ---
        // Prioridad al resolver el transporte de correo: Client > Tenant > Brevo de la plataforma.
        // Si estos campos están vacíos, el envío cae al SMTP del Tenant y luego al de la plataforma.
        public string? SmtpHost { get; set; }
        public int? SmtpPort { get; set; }
        public string? SmtpUser { get; set; }
        public string? SmtpPasswordEncrypted { get; set; }
        public bool SmtpUseSsl { get; set; } = true;
        public string? SmtpFromEmail { get; set; }
        public string? SmtpFromName { get; set; }

        // Consecutivo interno de Notas Crédito/Débito — separado del NextNumber de la Resolución
        // de Factura porque las notas no tienen rango autorizado propio ante la DIAN (solo
        // reutilizan el Prefix de la resolución "FE", ver DianDocumentMapper.BuildCreditNoteData).
        // Antes se reclamaba con ResolutionNumbering.ClaimNextNumberAsync(resolution.Id) sobre la
        // MISMA resolución de Factura, lo que hacía que cada nota consumiera un número que le
        // correspondía a la siguiente factura real.
        public long? NextCreditNoteNumber { get; set; }
        public long? NextDebitNoteNumber { get; set; }

        public DateTime CreatedAt { get; set; }
        public bool IsActive { get; set; }

        // Client de prueba auto-provisionado para el portal de developers — nunca es un cliente
        // real del Tenant, así que se excluye de los listados normales de Clients (ver
        // TenantClientsController.GetClients) para no confundirlo con la cartera real. Un
        // developer independiente tiene el suyo propio bajo el Tenant Sandbox compartido; un
        // developer invitado por un Tenant comparte uno solo por Tenant (ver DeveloperAuthController).
        public bool IsDeveloperSandbox { get; set; } = false;

        // --- Eventos de Recepción (RADIAN) ---
        // Conexión IMAP al correo de facturación electrónica del Client, para bajar automáticamente
        // las facturas/notas de sus proveedores. Para Gmail/Outlook, ReceptionEmailPasswordEncrypted
        // debe ser una "contraseña de aplicación" (esos proveedores ya no aceptan la clave normal
        // por IMAP), no la contraseña real de la cuenta.
        public bool ReceptionEmailEnabled { get; set; } = false;
        public string ReceptionEmailHost { get; set; } = string.Empty;
        public int ReceptionEmailPort { get; set; } = 993;
        public bool ReceptionEmailUseSsl { get; set; } = true;
        public string ReceptionEmailUser { get; set; } = string.Empty;
        public string ReceptionEmailPasswordEncrypted { get; set; } = string.Empty;

        // Cuáles eventos RADIAN se disparan automáticamente al recibir un documento (por correo o
        // carga manual). Todos son opcionales — el Client decide, incluido Reclamo.
        public bool AutoSendAcuseRecibo { get; set; } = false;
        public bool AutoSendReciboBien { get; set; } = false;
        public bool AutoSendAceptacion { get; set; } = false;
        public bool AutoSendReclamo { get; set; } = false;

        public ICollection<Resolution> Resolutions { get; set; } = new List<Resolution>();
        public ICollection<Certificate> Certificates { get; set; } = new List<Certificate>();
        public ICollection<Document> Documents { get; set; } = new List<Document>();
        public ICollection<ClientUser> Users { get; set; } = new List<ClientUser>();
        public ICollection<Customer> Customers { get; set; } = new List<Customer>();
        public ICollection<Product> Products { get; set; } = new List<Product>();
    }
}
