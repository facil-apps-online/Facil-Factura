using Fel.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fel.Infrastructure.Migrations
{
    // Habilita el Documento Soporte (DS) y su Nota de Ajuste (DS-AJUSTE) en todos los clientes que aún no los tienen: la pantalla
    // "Diseño de mis Facturas" solo lista los tipos habilitados, así que sin esto no podían elegir la plantilla que ahora se exige para emitir.
    // Con el correo al destinatario activado, como en los demás tipos que no son de nómina. Los clientes nuevos ya los traen por defecto
    // (DefaultCatalogSets.StandardDocumentTypeIds).
    [DbContext(typeof(FelDbContext))]
    [Migration("20261026210000_EnableSupportDocumentTypesForAllClients")]
    public partial class EnableSupportDocumentTypesForAllClients : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var documentTypeId in new[] { "00000000-0000-0000-0000-000000000024", "00000000-0000-0000-0000-000000000025" })
            {
                migrationBuilder.Sql($@"
INSERT INTO dbo.ClientEnabledDocumentTypes (Id, ClientId, DocumentTypeId, SendEmailToRecipient)
SELECT NEWID(), c.Id, '{documentTypeId}', 1
FROM dbo.Clients c
WHERE EXISTS (SELECT 1 FROM dbo.DocumentTypes d WHERE d.Id = '{documentTypeId}')
  AND NOT EXISTS (SELECT 1 FROM dbo.ClientEnabledDocumentTypes e WHERE e.ClientId = c.Id AND e.DocumentTypeId = '{documentTypeId}');");
            }
        }

        // Datos de configuración: no se deshace (quitaría también los que un tenant habilitó a propósito).
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
