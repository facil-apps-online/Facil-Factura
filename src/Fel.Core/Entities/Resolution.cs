using System;

namespace Fel.Core.Entities
{
    public class Resolution
    {
        public Guid Id { get; set; }
        public Guid ClientId { get; set; }
        public Client Client { get; set; } = null!;
        
        public string ResolutionNumber { get; set; } = string.Empty;
        public string Prefix { get; set; } = string.Empty;
        public long NumberStart { get; set; }
        public long NumberEnd { get; set; }

        // Próximo consecutivo a asignar al publicar un documento bajo esta resolución. Null hasta
        // que se emita el primer documento (o el tenant/cliente lo edite manualmente, ej. al migrar
        // desde otro sistema con numeración ya avanzada) — en ese caso se arranca desde NumberStart.
        public long? NextNumber { get; set; }
        public DateTime ValidFrom { get; set; }
        public DateTime ValidTo { get; set; }
        public string TechnicalKey { get; set; } = string.Empty; // Clave técnica DIAN

        // Tipo de documento (ej. FE, NC, ND, POS, NE). NE (Nómina Electrónica) no tiene resolución
        // DIAN real — se reutiliza esta misma fila solo para llevar Prefix/NumberStart/NextNumber,
        // ya que el consecutivo de nómina lo administra libremente el empleador (Anexo Técnico
        // Nómina Electrónica, campo Consecutivo).
        public string DocumentType { get; set; } = string.Empty;
        public bool IsActive { get; set; }

        // Cuál resolución usar por defecto cuando un cliente tiene varias activas del mismo
        // DocumentType (ej. varios prefijos) y no elige una explícitamente al emitir.
        public bool IsDefault { get; set; }
    }
}
