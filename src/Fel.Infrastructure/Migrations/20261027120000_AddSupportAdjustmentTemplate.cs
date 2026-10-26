using Fel.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fel.Infrastructure.Migrations
{
    // Plantilla global publicada de la nota de ajuste del documento soporte (DS-AJUSTE). Es un diseño propio, generado con
    // FacilReports/tools/gen_nota_ajuste_documento_soporte.py y subido a Facil Reports con esta clave: los cambios a la factura no la afectan.
    // Sin ninguna plantilla publicada para el tipo, el cliente no podía elegir una y la emisión por Dataico se frenaba.
    [DbContext(typeof(FelDbContext))]
    [Migration("20261027120000_AddSupportAdjustmentTemplate")]
    public partial class AddSupportAdjustmentTemplate : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM dbo.DocumentTypes WHERE Id = '00000000-0000-0000-0000-000000000025')
   AND NOT EXISTS (SELECT 1 FROM dbo.DocumentTemplates WHERE DocumentTypeId = '00000000-0000-0000-0000-000000000025' AND TenantId IS NULL AND ClientId IS NULL AND RepxTemplateKey = N'facilfactura_ds-ajuste_conlogo_05807a74e1012b2b')
    INSERT INTO dbo.DocumentTemplates (Id, Name, RepxTemplateKey, Status, VersionNumber, PreviousVersionId, ClonedFromId, DocumentTypeId, TenantId, ClientId, CreatedAt, UpdatedAt, MostrarRetenciones)
    VALUES (NEWID(), N'Nota de Ajuste - Con Logo (v2 JSON)', N'facilfactura_ds-ajuste_conlogo_05807a74e1012b2b', 1, 1, NULL, NULL, '00000000-0000-0000-0000-000000000025', NULL, NULL, SYSUTCDATETIME(), NULL, 1);");
        }

        // Dato de catálogo que los clientes pueden tener ya seleccionado: no se deshace.
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
