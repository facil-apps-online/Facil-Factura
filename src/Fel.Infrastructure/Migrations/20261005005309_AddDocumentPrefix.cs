using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentPrefix : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Prefix",
                table: "Documents",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            // Los documentos que ya existen quedan con el prefijo de la resolución a la que están ligados, que es el
            // que mostraban hasta ahora. Los borradores sin resolución quedan en null hasta que se publiquen.
            migrationBuilder.Sql(@"
                UPDATE d
                SET d.Prefix = r.Prefix
                FROM Documents d
                INNER JOIN Resolutions r ON r.Id = d.ResolutionId
                WHERE d.Prefix IS NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Prefix",
                table: "Documents");
        }
    }
}
