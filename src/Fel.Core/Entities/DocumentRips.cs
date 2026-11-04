using System;

namespace Fel.Core.Entities
{
    // RIPS soporte de una factura del sector salud y el resultado de su validación en el Mecanismo Único de Validación del Ministerio de Salud.
    // Una fila por documento. El RIPS se guarda tal como lo envió el integrador para poder corregirlo y reenviarlo sin volver a emitir la factura.
    public class DocumentRips
    {
        public Guid DocumentId { get; set; }
        public Document Document { get; set; } = null!;

        // Estados: PENDING (guardado, sin enviar), ACCEPTED (el Ministerio entregó el CUV) o REJECTED (el Ministerio lo rechazó con glosas).
        public const string Pending = "PENDING";
        public const string Accepted = "ACCEPTED";
        public const string Rejected = "REJECTED";

        public string RipsJson { get; set; } = "{}";
        public string Status { get; set; } = Pending;
        // Código Único de Validación (96 hexadecimales); solo con ACCEPTED.
        public string? Cuv { get; set; }
        // Consecutivo del envío (ProcesoId) que asigna el Ministerio.
        public long? ProcessId { get; set; }
        // Mensaje legible del último intento (rechazos, error de transporte o confirmación).
        public string? Message { get; set; }
        // Respuesta del Ministerio, con las glosas (ResultadosValidacion), tal como llegó.
        public string? ResponseJson { get; set; }
        public int Attempts { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? SentAt { get; set; }
    }
}
