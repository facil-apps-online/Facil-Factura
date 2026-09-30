using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Fel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFormaPagoCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "DataicoTaxCatalogItems",
                columns: new[] { "Id", "Category", "CreatedAt", "IsActive", "Kind", "Name", "Rate" },
                values: new object[,]
                {
                    { new Guid("10000000-0000-0000-0000-000000000013"), "DEBITO", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, 11, "Contado", null },
                    { new Guid("10000000-0000-0000-0000-000000000014"), "CREDITO", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, 11, "Crédito", null }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000013"));

            migrationBuilder.DeleteData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000014"));
        }
    }
}
