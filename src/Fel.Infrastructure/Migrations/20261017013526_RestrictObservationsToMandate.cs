using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RestrictObservationsToMandate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // La lista de observaciones es solo de la factura de mandato; cualquier otra factura o nota usa la observación general
            // (Documents.Notes). Las observaciones que la migración anterior pasó a la tabla para documentos que no son de mandato
            // vuelven al campo general (si hubiera varias, unidas con " / ") y se borran de la tabla.
            migrationBuilder.Sql(@"
UPDATE d SET Notes = ISNULL((
    SELECT STRING_AGG(CAST(o.Text AS nvarchar(max)), N' / ') WITHIN GROUP (ORDER BY o.Position)
    FROM DocumentObservations o WHERE o.DocumentId = d.Id), N'')
FROM Documents d
WHERE d.TypeCode <> 'FE-MANDATO'
  AND EXISTS (SELECT 1 FROM DocumentObservations o WHERE o.DocumentId = d.Id);

DELETE o FROM DocumentObservations o
JOIN Documents d ON d.Id = o.DocumentId
WHERE d.TypeCode <> 'FE-MANDATO';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Sin reversa: devolver cada observación general a la tabla no tiene un efecto distinto al de la migración anterior,
            // que ya se revierte por sí sola (AddDocumentObservationsAndMandate.Down).
        }
    }
}
