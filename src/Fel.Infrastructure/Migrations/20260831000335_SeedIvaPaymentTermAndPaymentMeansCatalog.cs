using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Fel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SeedIvaPaymentTermAndPaymentMeansCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "DataicoTaxCatalogItems",
                columns: new[] { "Id", "Category", "CreatedAt", "IsActive", "Kind", "Name" },
                values: new object[,]
                {
                    { new Guid("10000000-0000-0000-0000-000000000007"), "19", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, 2, "IVA General (19%)" },
                    { new Guid("10000000-0000-0000-0000-000000000008"), "5", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, 2, "IVA Reducido (5%)" },
                    { new Guid("10000000-0000-0000-0000-000000000009"), "0", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, 3, "Contado (0 días)" },
                    { new Guid("10000000-0000-0000-0000-00000000000a"), "15", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, 3, "15 días" },
                    { new Guid("10000000-0000-0000-0000-00000000000b"), "30", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, 3, "30 días" },
                    { new Guid("10000000-0000-0000-0000-00000000000c"), "45", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, 3, "45 días" },
                    { new Guid("10000000-0000-0000-0000-00000000000d"), "60", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, 3, "60 días" },
                    { new Guid("10000000-0000-0000-0000-00000000000e"), "90", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, 3, "90 días" },
                    { new Guid("10000000-0000-0000-0000-00000000000f"), "EFECTIVO", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, 4, "Efectivo" },
                    { new Guid("10000000-0000-0000-0000-000000000010"), "TRANSFERENCIA", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, 4, "Transferencia Bancaria" },
                    { new Guid("10000000-0000-0000-0000-000000000011"), "DEBIT_CARD", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, 4, "Tarjeta Débito" },
                    { new Guid("10000000-0000-0000-0000-000000000012"), "CREDIT_CARD", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, 4, "Tarjeta Crédito" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000007"));

            migrationBuilder.DeleteData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000008"));

            migrationBuilder.DeleteData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000009"));

            migrationBuilder.DeleteData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-00000000000a"));

            migrationBuilder.DeleteData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-00000000000b"));

            migrationBuilder.DeleteData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-00000000000c"));

            migrationBuilder.DeleteData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-00000000000d"));

            migrationBuilder.DeleteData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-00000000000e"));

            migrationBuilder.DeleteData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-00000000000f"));

            migrationBuilder.DeleteData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000010"));

            migrationBuilder.DeleteData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000011"));

            migrationBuilder.DeleteData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000012"));
        }
    }
}
