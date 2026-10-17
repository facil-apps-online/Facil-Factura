using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCurrencyWordsName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "WordsName",
                table: "TaxCatalogItems",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.UpdateData(
                table: "TaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000001"),
                column: "WordsName",
                value: "DÓLARES");

            migrationBuilder.UpdateData(
                table: "TaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000002"),
                column: "WordsName",
                value: "EUROS");

            // COP entra al catálogo de monedas: su nombre en letras ("PESOS") sale de aquí, igual que el de las demás.
            migrationBuilder.InsertData(
                table: "TaxCatalogItems",
                columns: new[] { "Id", "Category", "CreatedAt", "DianCode", "IsActive", "Kind", "Name", "Rate", "WordsName" },
                values: new object[] { new Guid("a0000000-0000-0000-0000-000000000003"), "COP", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, true, 14, "Peso colombiano", null, "PESOS" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "TaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000003"));

            migrationBuilder.DropColumn(
                name: "WordsName",
                table: "TaxCatalogItems");
        }
    }
}
