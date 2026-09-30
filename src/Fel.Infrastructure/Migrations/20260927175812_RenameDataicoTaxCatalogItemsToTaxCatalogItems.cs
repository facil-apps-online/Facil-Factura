using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RenameDataicoTaxCatalogItemsToTaxCatalogItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Solo renombra la tabla (metadata, sp_rename) — EF había scaffoldeado esto como
            // DropTable + CreateTable + reinsertar el seed original, lo que habría borrado
            // cualquier fila agregada/editada por Superadmin desde el seed inicial. Se corrigió
            // a mano para no perder datos.
            migrationBuilder.RenameTable(
                name: "DataicoTaxCatalogItems",
                newName: "TaxCatalogItems");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameTable(
                name: "TaxCatalogItems",
                newName: "DataicoTaxCatalogItems");
        }
    }
}
