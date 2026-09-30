using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Fel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUnitOfMeasureCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UnitOfMeasure",
                table: "Products");

            // Filas existentes (Products/DocumentItems ya guardados) deben apuntar/mostrar "Unidad"
            // (94/EA) — el mismo valor que ya se usaba de facto antes de este catálogo — no
            // Guid.Empty ni cadenas vacías, que romperían la FK o dejarían el detalle en blanco en
            // facturas ya emitidas.
            migrationBuilder.AddColumn<Guid>(
                name: "UnitOfMeasureId",
                table: "Products",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("30000000-0000-0000-0000-000000000001"));

            migrationBuilder.AddColumn<string>(
                name: "UnitOfMeasureAbbreviation",
                table: "DocumentItems",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "EA");

            migrationBuilder.AddColumn<string>(
                name: "UnitOfMeasureCode",
                table: "DocumentItems",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "94");

            migrationBuilder.AddColumn<string>(
                name: "UnitOfMeasureDisplayFormat",
                table: "DocumentItems",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "Combined");

            migrationBuilder.AddColumn<string>(
                name: "UnitOfMeasureDisplayOverride",
                table: "Clients",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "UnitsOfMeasure",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DianCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Abbreviation = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DisplayFormat = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UnitsOfMeasure", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "UnitsOfMeasure",
                columns: new[] { "Id", "Abbreviation", "CreatedAt", "DianCode", "DisplayFormat", "IsActive", "Name" },
                values: new object[,]
                {
                    { new Guid("30000000-0000-0000-0000-000000000001"), "EA", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "94", "Combined", true, "Unidad" },
                    { new Guid("30000000-0000-0000-0000-000000000002"), "KGM", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "KGM", "AbbreviationOnly", true, "Kilogramo" },
                    { new Guid("30000000-0000-0000-0000-000000000003"), "LBR", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "LBR", "AbbreviationOnly", true, "Libra" },
                    { new Guid("30000000-0000-0000-0000-000000000004"), "HUR", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "HUR", "AbbreviationOnly", true, "Hora" },
                    { new Guid("30000000-0000-0000-0000-000000000005"), "DAY", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "DAY", "AbbreviationOnly", true, "Día" },
                    { new Guid("30000000-0000-0000-0000-000000000006"), "ANA", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "ANA", "AbbreviationOnly", true, "Año" },
                    { new Guid("30000000-0000-0000-0000-000000000007"), "LUN", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "LUN", "AbbreviationOnly", true, "Mes" },
                    { new Guid("30000000-0000-0000-0000-000000000008"), "DZN", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "DZN", "AbbreviationOnly", true, "Docena" },
                    { new Guid("30000000-0000-0000-0000-000000000009"), "GLL", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "GLL", "AbbreviationOnly", true, "Galón" },
                    { new Guid("30000000-0000-0000-0000-000000000010"), "MTR", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "MTR", "AbbreviationOnly", true, "Metro" },
                    { new Guid("30000000-0000-0000-0000-000000000011"), "ZZ", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "ZZ", "AbbreviationOnly", true, "Mutuamente definido" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Products_UnitOfMeasureId",
                table: "Products",
                column: "UnitOfMeasureId");

            migrationBuilder.AddForeignKey(
                name: "FK_Products_UnitsOfMeasure_UnitOfMeasureId",
                table: "Products",
                column: "UnitOfMeasureId",
                principalTable: "UnitsOfMeasure",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Products_UnitsOfMeasure_UnitOfMeasureId",
                table: "Products");

            migrationBuilder.DropTable(
                name: "UnitsOfMeasure");

            migrationBuilder.DropIndex(
                name: "IX_Products_UnitOfMeasureId",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "UnitOfMeasureId",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "UnitOfMeasureAbbreviation",
                table: "DocumentItems");

            migrationBuilder.DropColumn(
                name: "UnitOfMeasureCode",
                table: "DocumentItems");

            migrationBuilder.DropColumn(
                name: "UnitOfMeasureDisplayFormat",
                table: "DocumentItems");

            migrationBuilder.DropColumn(
                name: "UnitOfMeasureDisplayOverride",
                table: "Clients");

            migrationBuilder.AddColumn<string>(
                name: "UnitOfMeasure",
                table: "Products",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }
    }
}
