using System;

namespace Fel.Core.Entities;

// Qué tipo de correo es: define el remitente cuando sale por la plataforma (SendPulse) y qué se hace con él al enviarse.
public enum EmailKind
{
    Document = 1,            // Documento electrónico o su reenvío
    Certificate = 2,         // Certificado (.p12 comprimido)
    CertificatePassword = 3, // Contraseña del certificado
    Notification = 4,        // Portal de clientes: alertas de plan
    Platform = 5             // Plataforma: usuarios de tenant y superadmin
}

public enum EmailOutboxStatus
{
    Pending = 1,
    Sending = 2,
    Sent = 3,
    Failed = 4,    // Falló pero se reintenta (NextAttemptAt)
    Dead = 5,      // Agotó los intentos
    Held = 6,      // Espera a que se envíe el correo del que depende (DependsOnId)
    Cancelled = 7  // El correo del que dependía no se pudo enviar
}

// Estado de entrega que reporta el proveedor: Delivered = el servidor del destinatario respondió 2xx; Bounced = lo rechazó (5xx).
public enum EmailDeliveryStatus
{
    Unknown = 0,
    Delivered = 1,
    Bounced = 2
}

// Bandeja de salida de los correos de la plataforma (patrón outbox): quien origina el correo inserta la fila en la misma transacción
// del cambio de negocio y el worker la envía al recibir el aviso por Redis; el barrido periódico solo recoge lo que se quedó sin aviso.
public class EmailOutbox
{
    public Guid Id { get; set; }
    public EmailKind Kind { get; set; }
    public EmailOutboxStatus Status { get; set; } = EmailOutboxStatus.Pending;
    public Guid? TenantId { get; set; }
    public Guid? ClientId { get; set; }
    public Client? Client { get; set; }
    public string RecipientEmail { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    // Cifrado con ICryptoVault cuando Sensitive (contiene la contraseña de un certificado); se borra al enviarse.
    public string BodyHtml { get; set; } = string.Empty;
    public bool Sensitive { get; set; }
    // JSON [{fileName, contentType, contentBase64}]; se borra al enviarse.
    public string? AttachmentsJson { get; set; }
    // Origen del correo, para mostrar su estado donde corresponde.
    public Guid? DocumentId { get; set; }
    public Guid? CertificateRequestId { get; set; }
    // Este correo solo sale cuando el indicado quedó enviado.
    public Guid? DependsOnId { get; set; }
    public int Attempts { get; set; }
    public DateTime NextAttemptAt { get; set; } = DateTime.UtcNow;
    public string LastError { get; set; } = string.Empty;
    // client-smtp | tenant-smtp | platform
    public string Channel { get; set; } = string.Empty;
    public string ProviderMessageId { get; set; } = string.Empty;
    // Lo que el proveedor reporta después del envío (solo canal platform): el servidor del destinatario aceptó o rechazó el correo y si lo abrió.
    public EmailDeliveryStatus DeliveryStatus { get; set; }
    public string DeliveryDetail { get; set; } = string.Empty;
    public DateTime? OpenedAt { get; set; }
    public DateTime? TrackedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? SentAt { get; set; }
}

// Configuración única de la plataforma para el envío de correo por SendPulse.
public class PlatformEmailSettings
{
    public int Id { get; set; } = 1;
    // Client ID de la API de SendPulse (Configuración > API); el secreto va cifrado con ICryptoVault y nunca se devuelve por la API.
    public string SendPulseClientId { get; set; } = string.Empty;
    public string SendPulseSecretEncrypted { get; set; } = string.Empty;
    // Tope de correos por hora que SendPulse admite en el plan contratado; lo que pasa de ahí espera a la siguiente hora.
    public int MaxPerHour { get; set; } = 50;
    public string DocumentsFromEmail { get; set; } = "facturacion@facil-factura.pro";
    public string CertificatesFromEmail { get; set; } = "certificados@facil-factura.pro";
    public string NotificationsFromEmail { get; set; } = "notificaciones@facil-factura.pro";
    public string PlatformFromEmail { get; set; } = "no-responder@facil-factura.pro";
    public string PlatformFromName { get; set; } = "Facil Factura";
    public string SupportEmail { get; set; } = string.Empty;
    // Colores de los correos de documentos cuando ni el cliente ni su tenant definieron los suyos. Nulos = el azul por defecto.
    public string? EmailColorLight { get; set; }
    public string? EmailColorDark { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public string UpdatedBy { get; set; } = string.Empty;
}
