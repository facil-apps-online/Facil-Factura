using System;

namespace Fel.Core.Models
{
    // Hora legal de Colombia (UTC-5, sin horario de verano). Los documentos electrónicos son de la
    // DIAN y su fecha de emisión se valida contra la hora colombiana, no contra UTC: con UtcNow, un
    // documento creado entre las 7 p. m. y la medianoche salía con la fecha del día siguiente.
    // Offset fijo en vez de TimeZoneInfo para no depender de la base de zonas horarias del sistema
    // (distinta en Windows y Linux/contenedor); Colombia no tiene cambio de hora.
    // Auditoría y vigencias (CreatedAt, ProcessedAt, tokens) siguen en UTC; esto es para lo que se
    // muestra o se envía como fecha del documento.
    public static class ColombiaTime
    {
        public static readonly TimeSpan Offset = TimeSpan.FromHours(-5);

        public static DateTime Now => FromUtc(DateTime.UtcNow);

        public static DateTime Today => Now.Date;

        public static DateTime FromUtc(DateTime utc) =>
            DateTime.SpecifyKind(DateTime.SpecifyKind(utc, DateTimeKind.Utc) + Offset, DateTimeKind.Unspecified);
    }
}
