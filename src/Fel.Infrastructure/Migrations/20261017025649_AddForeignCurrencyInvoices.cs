using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Fel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddForeignCurrencyInvoices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Currency",
                table: "Documents",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "COP");

            migrationBuilder.AddColumn<decimal>(
                name: "ExchangeRate",
                table: "Documents",
                type: "decimal(18,4)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ExchangeRateDate",
                table: "Documents",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ForeignCurrencyEnabled",
                table: "Clients",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.InsertData(
                table: "TaxCatalogItems",
                columns: new[] { "Id", "Category", "CreatedAt", "DianCode", "IsActive", "Kind", "Name", "Rate" },
                values: new object[,]
                {
                    { new Guid("a0000000-0000-0000-0000-000000000001"), "USD", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, true, 14, "Dólar estadounidense", null },
                    { new Guid("a0000000-0000-0000-0000-000000000002"), "EUR", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, true, 14, "Euro", null }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "TaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000001"));

            migrationBuilder.DeleteData(
                table: "TaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000002"));

            migrationBuilder.DropColumn(
                name: "Currency",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "ExchangeRate",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "ExchangeRateDate",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "ForeignCurrencyEnabled",
                table: "Clients");
        }
    }
}
