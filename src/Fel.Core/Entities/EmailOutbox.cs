using System;

namespace Fel.Core.Entities;

// Qué tipo de correo es: define el remitente cuando sale por la plataforma (Brevo) y qué se hace con él al enviarse.
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
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? SentAt { get; set; }
}

// Configuración única de la plataforma para el envío de correo por Brevo.
public class PlatformEmailSettings
{
    public int Id { get; set; } = 1;
    // Cifrada con ICryptoVault; nunca se devuelve por la API.
    public string BrevoApiKeyEncrypted { get; set; } = string.Empty;
    public string DocumentsFromEmail { get; set; } = "facturacion@facil-factura.pro";
    public string CertificatesFromEmail { get; set; } = "certificados@facil-factura.pro";
    public string NotificationsFromEmail { get; set; } = "notificaciones@facil-factura.pro";
    public string PlatformFromEmail { get; set; } = "no-responder@facil-factura.pro";
    public string PlatformFromName { get; set; } = "Facil Factura";
    public string SupportEmail { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public string UpdatedBy { get; set; } = string.Empty;
}
