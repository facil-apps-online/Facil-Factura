using Fel.Core.Entities;

namespace Fel.Core.Interfaces
{
    // Datos de un correo a encolar en la bandeja de salida.
    public class OutboundEmail
    {
        public EmailKind Kind { get; set; }
        public Client Client { get; set; } = null!;
        public string RecipientEmail { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public string BodyHtml { get; set; } = string.Empty;
        public IReadOnlyList<EmailAttachment>? Attachments { get; set; }
        public bool Sensitive { get; set; }
        public Guid? DocumentId { get; set; }
        public Guid? CertificateRequestId { get; set; }
        // Solo sale cuando este correo quedó enviado.
        public Guid? DependsOnId { get; set; }
    }

    /// <summary>
    /// Bandeja de salida: <see cref="Add"/> agrega la fila al contexto (se guarda con la transacción de negocio del llamador) y
    /// <see cref="NotifyAsync"/>, ya guardada, avisa al worker por Redis para que la envíe al instante.
    /// </summary>
    public interface IEmailOutbox
    {
        EmailOutbox Add(OutboundEmail email);
        Task NotifyAsync(params EmailOutbox[] emails);
    }

    // Aviso por Redis (Pub/Sub) de que hay un correo para enviar. Si el aviso se pierde, el barrido del worker lo recoge.
    public interface IEmailSignal
    {
        Task PublishAsync(Guid emailId);
        void Subscribe(Func<Guid, Task> handler);
    }
}
