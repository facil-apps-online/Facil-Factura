using System;
using System.Collections.Generic;

namespace Fel.Core.Entities
{
    public enum NoteKind
    {
        CreditNote = 1,
        DebitNote = 2,
        SupportAdjustment = 3
    }

    public static class NoteKinds
    {
        // Etiqueta de cada tipo de nota: la API la expone para que el portal no la escriba a mano.
        public static readonly IReadOnlyList<(NoteKind Kind, string Label)> All = new[]
        {
            (NoteKind.CreditNote, "Nota crédito"),
            (NoteKind.DebitNote, "Nota débito"),
            (NoteKind.SupportAdjustment, "Nota de ajuste")
        };
    }

    // Consecutivo (y prefijo) de un tipo de nota. Por defecto es uno solo por Client (BranchId nulo); una sucursal que necesite
    // numeración propia tiene su fila, y entonces el prefijo propio es obligatorio: el prefijo es único por Client y tipo
    // (la cuenta de Dataico y la DIAN identifican la numeración por prefijo), así que dos contadores nunca comparten uno.
    // Las notas no tienen rango autorizado ante la DIAN, por eso el consecutivo es interno.
    public class NoteNumbering
    {
        public Guid Id { get; set; }
        public Guid ClientId { get; set; }
        public Client Client { get; set; } = null!;

        // Null = contador compartido por todas las sucursales del Client.
        public Guid? BranchId { get; set; }
        public Branch? Branch { get; set; }

        public NoteKind Kind { get; set; }

        // Null = el documento usa el prefijo de su resolución (notas crédito y débito) o el de su resolución de respaldo
        // (nota de ajuste del soporte). En una fila de sucursal siempre trae valor.
        public string? Prefix { get; set; }

        // Próximo consecutivo a asignar. Null = aún no se emitió ninguna (arranca en 1).
        public long? NextNumber { get; set; }

        public static NoteNumbering Shared(Guid clientId, NoteKind kind, string? prefix = null, long? nextNumber = null) => new NoteNumbering
        {
            Id = Guid.NewGuid(),
            ClientId = clientId,
            Kind = kind,
            Prefix = prefix,
            NextNumber = nextNumber
        };
    }
}
