using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Fel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDataicoTaxCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DataicoTaxCatalogItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Category = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DataicoTaxCatalogItems", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "DataicoTaxCatalogItems",
                columns: new[] { "Id", "Category", "CreatedAt", "IsActive", "Kind", "Name" },
                values: new object[,]
                {
                    { new Guid("10000000-0000-0000-0000-000000000001"), "RET_FUENTE", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, 0, "Retención en la Fuente" },
                    { new Guid("10000000-0000-0000-0000-000000000002"), "RET_ICA", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, 0, "Retención de ICA" },
                    { new Guid("10000000-0000-0000-0000-000000000003"), "RET_IVA", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, 0, "Retención de IVA" },
                    { new Guid("10000000-0000-0000-0000-000000000004"), "IMP_CONSUMO", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, 1, "Impuesto al Consumo" },
                    { new Guid("10000000-0000-0000-0000-000000000005"), "IMP_CONSUMO_LICOR", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, 1, "Impuesto al Consumo de Licores" },
                    { new Guid("10000000-0000-0000-0000-000000000006"), "IMP_BOLSA_PLASTICA", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, 1, "Impuesto a la Bolsa Plástica" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DataicoTaxCatalogItems");
        }
    }
}
