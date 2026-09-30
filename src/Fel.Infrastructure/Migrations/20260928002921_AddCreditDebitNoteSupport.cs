using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Fel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCreditDebitNoteSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DiscrepancyResponseCode",
                table: "Documents",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.InsertData(
                table: "TaxCatalogItems",
                columns: new[] { "Id", "Category", "CreatedAt", "DianCode", "IsActive", "Kind", "Name", "Rate" },
                values: new object[,]
                {
                    { new Guid("80000000-0000-0000-0000-000000000001"), "1", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "DEVOLUCION", true, 12, "Devolución parcial de los bienes y/o no aceptación parcial del servicio", null },
                    { new Guid("80000000-0000-0000-0000-000000000002"), "2", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "ANULACION", true, 12, "Anulación de factura electrónica", null },
                    { new Guid("80000000-0000-0000-0000-000000000003"), "3", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "OTROS", true, 12, "Rebaja o descuento parcial o total", null },
                    { new Guid("80000000-0000-0000-0000-000000000004"), "4", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "OTROS", true, 12, "Ajuste de precio", null },
                    { new Guid("80000000-0000-0000-0000-000000000005"), "5", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "OTROS", true, 12, "Descuento comercial por pronto pago", null },
                    { new Guid("80000000-0000-0000-0000-000000000006"), "6", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "OTROS", true, 12, "Descuento comercial por volumen de ventas", null },
                    { new Guid("90000000-0000-0000-0000-000000000001"), "1", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "OTROS", true, 13, "Intereses", null },
                    { new Guid("90000000-0000-0000-0000-000000000002"), "2", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "OTROS", true, 13, "Gastos por cobrar", null },
                    { new Guid("90000000-0000-0000-0000-000000000003"), "3", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "OTROS", true, 13, "Cambio del valor", null },
                    { new Guid("90000000-0000-0000-0000-000000000004"), "4", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "OTROS", true, 13, "Otros", null }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "TaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("80000000-0000-0000-0000-000000000001"));

            migrationBuilder.DeleteData(
                table: "TaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("80000000-0000-0000-0000-000000000002"));

            migrationBuilder.DeleteData(
                table: "TaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("80000000-0000-0000-0000-000000000003"));

            migrationBuilder.DeleteData(
                table: "TaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("80000000-0000-0000-0000-000000000004"));

            migrationBuilder.DeleteData(
                table: "TaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("80000000-0000-0000-0000-000000000005"));

            migrationBuilder.DeleteData(
                table: "TaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("80000000-0000-0000-0000-000000000006"));

            migrationBuilder.DeleteData(
                table: "TaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("90000000-0000-0000-0000-000000000001"));

            migrationBuilder.DeleteData(
                table: "TaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("90000000-0000-0000-0000-000000000002"));

            migrationBuilder.DeleteData(
                table: "TaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("90000000-0000-0000-0000-000000000003"));

            migrationBuilder.DeleteData(
                table: "TaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("90000000-0000-0000-0000-000000000004"));

            migrationBuilder.DropColumn(
                name: "DiscrepancyResponseCode",
                table: "Documents");
        }
    }
}
