using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentObservationsAndMandate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "MandanteCustomerId",
                table: "DocumentItems",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MandanteIdentification",
                table: "DocumentItems",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MandanteIdentificationType",
                table: "DocumentItems",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MandanteName",
                table: "DocumentItems",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DocumentObservations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Position = table.Column<int>(type: "int", nullable: false),
                    Text = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentObservations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentObservations_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentItems_MandanteCustomerId",
                table: "DocumentItems",
                column: "MandanteCustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentObservations_DocumentId_Position",
                table: "DocumentObservations",
                columns: new[] { "DocumentId", "Position" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_DocumentItems_Customers_MandanteCustomerId",
                table: "DocumentItems",
                column: "MandanteCustomerId",
                principalTable: "Customers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // Las observaciones actuales de facturas y notas (el campo de una línea que había hasta hoy) pasan a la tabla nueva como
            // la observación 1 de cada documento, y el campo viejo queda vacío para que no haya dos fuentes del mismo dato. El
            // documento soporte y la nómina siguen usando Documents.Notes. El filtro de tipos es el mismo de InvoiceController.InvoiceDocuments.
            migrationBuilder.Sql(@"
INSERT INTO DocumentObservations (Id, DocumentId, Position, Text)
SELECT NEWID(), Id, 1, LEFT(LTRIM(RTRIM(Notes)), 500)
FROM Documents
WHERE ((TypeCode LIKE 'FE%' AND TypeCode <> 'FE-TEST') OR TypeCode IN ('NC', 'ND'))
  AND LTRIM(RTRIM(ISNULL(Notes, ''))) <> '';

UPDATE Documents SET Notes = ''
WHERE ((TypeCode LIKE 'FE%' AND TypeCode <> 'FE-TEST') OR TypeCode IN ('NC', 'ND'))
  AND LTRIM(RTRIM(ISNULL(Notes, ''))) <> '';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Devuelve las observaciones al campo de texto, una por línea, antes de borrar la tabla.
            migrationBuilder.Sql(@"
UPDATE d SET Notes = ISNULL((
    SELECT STRING_AGG(CAST(o.Text AS nvarchar(max)), CHAR(10)) WITHIN GROUP (ORDER BY o.Position)
    FROM DocumentObservations o WHERE o.DocumentId = d.Id), '')
FROM Documents d
WHERE (d.TypeCode LIKE 'FE%' AND d.TypeCode <> 'FE-TEST') OR d.TypeCode IN ('NC', 'ND');");

            migrationBuilder.DropForeignKey(
                name: "FK_DocumentItems_Customers_MandanteCustomerId",
                table: "DocumentItems");

            migrationBuilder.DropTable(
                name: "DocumentObservations");

            migrationBuilder.DropIndex(
                name: "IX_DocumentItems_MandanteCustomerId",
                table: "DocumentItems");

            migrationBuilder.DropColumn(
                name: "MandanteCustomerId",
                table: "DocumentItems");

            migrationBuilder.DropColumn(
                name: "MandanteIdentification",
                table: "DocumentItems");

            migrationBuilder.DropColumn(
                name: "MandanteIdentificationType",
                table: "DocumentItems");

            migrationBuilder.DropColumn(
                name: "MandanteName",
                table: "DocumentItems");
        }
    }
}
