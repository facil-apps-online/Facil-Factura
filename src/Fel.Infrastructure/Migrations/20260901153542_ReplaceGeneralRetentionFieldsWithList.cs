using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ReplaceGeneralRetentionFieldsWithList : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DocumentGeneralRetentions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TaxCategory = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Rate = table.Column<decimal>(type: "decimal(6,3)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentGeneralRetentions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentGeneralRetentions_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentGeneralRetentions_DocumentId",
                table: "DocumentGeneralRetentions",
                column: "DocumentId");

            // Respaldo de cualquier retención general ya guardada con el modelo viejo (2 campos fijos)
            // antes de borrar las columnas, para no perder borradores existentes.
            migrationBuilder.Sql(@"
                INSERT INTO DocumentGeneralRetentions (Id, DocumentId, TaxCategory, Rate)
                SELECT NEWID(), Id, GeneralReteIcaCategory, GeneralReteIcaRate
                FROM Documents
                WHERE GeneralReteIcaCategory IS NOT NULL AND GeneralReteIcaRate IS NOT NULL;

                INSERT INTO DocumentGeneralRetentions (Id, DocumentId, TaxCategory, Rate)
                SELECT NEWID(), Id, GeneralReteIvaCategory, GeneralReteIvaRate
                FROM Documents
                WHERE GeneralReteIvaCategory IS NOT NULL AND GeneralReteIvaRate IS NOT NULL;
            ");

            migrationBuilder.DropColumn(
                name: "GeneralReteIcaCategory",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "GeneralReteIcaRate",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "GeneralReteIvaCategory",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "GeneralReteIvaRate",
                table: "Documents");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DocumentGeneralRetentions");

            migrationBuilder.AddColumn<string>(
                name: "GeneralReteIcaCategory",
                table: "Documents",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "GeneralReteIcaRate",
                table: "Documents",
                type: "decimal(6,3)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GeneralReteIvaCategory",
                table: "Documents",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "GeneralReteIvaRate",
                table: "Documents",
                type: "decimal(6,3)",
                nullable: true);
        }
    }
}
