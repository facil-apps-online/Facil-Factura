using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Fel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCatalogRateAndGeneralRetentions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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

            migrationBuilder.AddColumn<decimal>(
                name: "Rate",
                table: "DataicoTaxCatalogItems",
                type: "decimal(6,3)",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000001"),
                columns: new[] { "IsActive", "Rate" },
                values: new object[] { false, null });

            migrationBuilder.UpdateData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000002"),
                columns: new[] { "IsActive", "Rate" },
                values: new object[] { false, null });

            migrationBuilder.UpdateData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000003"),
                columns: new[] { "IsActive", "Rate" },
                values: new object[] { false, null });

            migrationBuilder.UpdateData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000004"),
                column: "Rate",
                value: null);

            migrationBuilder.UpdateData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000005"),
                column: "Rate",
                value: null);

            migrationBuilder.UpdateData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000006"),
                column: "Rate",
                value: null);

            migrationBuilder.UpdateData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000007"),
                column: "Rate",
                value: null);

            migrationBuilder.UpdateData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000008"),
                column: "Rate",
                value: null);

            migrationBuilder.UpdateData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000009"),
                column: "Rate",
                value: null);

            migrationBuilder.UpdateData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-00000000000a"),
                column: "Rate",
                value: null);

            migrationBuilder.UpdateData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-00000000000b"),
                column: "Rate",
                value: null);

            migrationBuilder.UpdateData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-00000000000c"),
                column: "Rate",
                value: null);

            migrationBuilder.UpdateData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-00000000000d"),
                column: "Rate",
                value: null);

            migrationBuilder.UpdateData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-00000000000e"),
                column: "Rate",
                value: null);

            migrationBuilder.UpdateData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-00000000000f"),
                column: "Rate",
                value: null);

            migrationBuilder.UpdateData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000010"),
                column: "Rate",
                value: null);

            migrationBuilder.UpdateData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000011"),
                column: "Rate",
                value: null);

            migrationBuilder.UpdateData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000012"),
                column: "Rate",
                value: null);

            migrationBuilder.UpdateData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000001"),
                column: "Rate",
                value: null);

            migrationBuilder.UpdateData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000002"),
                column: "Rate",
                value: null);

            migrationBuilder.UpdateData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000003"),
                column: "Rate",
                value: null);

            migrationBuilder.UpdateData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000004"),
                column: "Rate",
                value: null);

            migrationBuilder.UpdateData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000005"),
                column: "Rate",
                value: null);

            migrationBuilder.UpdateData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000006"),
                column: "Rate",
                value: null);

            migrationBuilder.UpdateData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000007"),
                column: "Rate",
                value: null);

            migrationBuilder.UpdateData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000008"),
                column: "Rate",
                value: null);

            migrationBuilder.UpdateData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000009"),
                column: "Rate",
                value: null);

            migrationBuilder.UpdateData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000010"),
                column: "Rate",
                value: null);

            migrationBuilder.UpdateData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000011"),
                column: "Rate",
                value: null);

            migrationBuilder.UpdateData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000012"),
                column: "Rate",
                value: null);

            migrationBuilder.UpdateData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000013"),
                column: "Rate",
                value: null);

            migrationBuilder.UpdateData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000014"),
                column: "Rate",
                value: null);

            migrationBuilder.UpdateData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000015"),
                column: "Rate",
                value: null);

            migrationBuilder.UpdateData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000016"),
                column: "Rate",
                value: null);

            migrationBuilder.UpdateData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000017"),
                column: "Rate",
                value: null);

            migrationBuilder.UpdateData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000018"),
                column: "Rate",
                value: null);

            migrationBuilder.UpdateData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000001"),
                column: "Rate",
                value: null);

            migrationBuilder.UpdateData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000002"),
                column: "Rate",
                value: null);

            migrationBuilder.UpdateData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000003"),
                column: "Rate",
                value: null);

            migrationBuilder.UpdateData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000004"),
                column: "Rate",
                value: null);

            migrationBuilder.UpdateData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000005"),
                column: "Rate",
                value: null);

            migrationBuilder.UpdateData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000001"),
                column: "Rate",
                value: null);

            migrationBuilder.UpdateData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000002"),
                column: "Rate",
                value: null);

            migrationBuilder.UpdateData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000003"),
                column: "Rate",
                value: null);

            migrationBuilder.UpdateData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000004"),
                column: "Rate",
                value: null);

            migrationBuilder.UpdateData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000005"),
                column: "Rate",
                value: null);

            migrationBuilder.UpdateData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000006"),
                column: "Rate",
                value: null);

            migrationBuilder.UpdateData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000007"),
                column: "Rate",
                value: null);

            migrationBuilder.UpdateData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000008"),
                column: "Rate",
                value: null);

            migrationBuilder.UpdateData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000009"),
                column: "Rate",
                value: null);

            migrationBuilder.UpdateData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000010"),
                column: "Rate",
                value: null);

            migrationBuilder.UpdateData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000011"),
                column: "Rate",
                value: null);

            migrationBuilder.UpdateData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000012"),
                column: "Rate",
                value: null);

            migrationBuilder.UpdateData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000013"),
                column: "Rate",
                value: null);

            migrationBuilder.UpdateData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000014"),
                column: "Rate",
                value: null);

            migrationBuilder.UpdateData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000015"),
                column: "Rate",
                value: null);

            migrationBuilder.UpdateData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000016"),
                column: "Rate",
                value: null);

            migrationBuilder.UpdateData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000017"),
                column: "Rate",
                value: null);

            migrationBuilder.UpdateData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000018"),
                column: "Rate",
                value: null);

            migrationBuilder.UpdateData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000019"),
                column: "Rate",
                value: null);

            migrationBuilder.UpdateData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000020"),
                column: "Rate",
                value: null);

            migrationBuilder.UpdateData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000021"),
                column: "Rate",
                value: null);

            migrationBuilder.UpdateData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000022"),
                column: "Rate",
                value: null);

            migrationBuilder.UpdateData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000023"),
                column: "Rate",
                value: null);

            migrationBuilder.UpdateData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000024"),
                column: "Rate",
                value: null);

            migrationBuilder.UpdateData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000025"),
                column: "Rate",
                value: null);

            migrationBuilder.UpdateData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000026"),
                column: "Rate",
                value: null);

            migrationBuilder.UpdateData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000027"),
                column: "Rate",
                value: null);

            migrationBuilder.UpdateData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000028"),
                column: "Rate",
                value: null);

            migrationBuilder.UpdateData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000029"),
                column: "Rate",
                value: null);

            migrationBuilder.UpdateData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000030"),
                column: "Rate",
                value: null);

            migrationBuilder.UpdateData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000031"),
                column: "Rate",
                value: null);

            migrationBuilder.UpdateData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000032"),
                column: "Rate",
                value: null);

            migrationBuilder.UpdateData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000033"),
                column: "Rate",
                value: null);

            migrationBuilder.UpdateData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000034"),
                column: "Rate",
                value: null);

            migrationBuilder.UpdateData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000035"),
                column: "Rate",
                value: null);

            migrationBuilder.UpdateData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000036"),
                column: "Rate",
                value: null);

            migrationBuilder.UpdateData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000037"),
                column: "Rate",
                value: null);

            migrationBuilder.UpdateData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000038"),
                column: "Rate",
                value: null);

            migrationBuilder.UpdateData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000039"),
                column: "Rate",
                value: null);

            migrationBuilder.UpdateData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000040"),
                column: "Rate",
                value: null);

            migrationBuilder.UpdateData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000041"),
                column: "Rate",
                value: null);

            migrationBuilder.UpdateData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000042"),
                column: "Rate",
                value: null);

            migrationBuilder.UpdateData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000043"),
                column: "Rate",
                value: null);

            migrationBuilder.UpdateData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000044"),
                column: "Rate",
                value: null);

            migrationBuilder.UpdateData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000045"),
                column: "Rate",
                value: null);

            migrationBuilder.UpdateData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000046"),
                column: "Rate",
                value: null);

            migrationBuilder.UpdateData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000047"),
                column: "Rate",
                value: null);

            migrationBuilder.UpdateData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000048"),
                column: "Rate",
                value: null);

            migrationBuilder.UpdateData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000049"),
                column: "Rate",
                value: null);

            migrationBuilder.UpdateData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000050"),
                column: "Rate",
                value: null);

            migrationBuilder.UpdateData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000051"),
                column: "Rate",
                value: null);

            migrationBuilder.UpdateData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000052"),
                column: "Rate",
                value: null);

            migrationBuilder.UpdateData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000053"),
                column: "Rate",
                value: null);

            migrationBuilder.UpdateData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("50000000-0000-0000-0000-000000000001"),
                column: "Rate",
                value: null);

            migrationBuilder.UpdateData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("50000000-0000-0000-0000-000000000002"),
                column: "Rate",
                value: null);

            migrationBuilder.UpdateData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("50000000-0000-0000-0000-000000000003"),
                column: "Rate",
                value: null);

            migrationBuilder.UpdateData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("50000000-0000-0000-0000-000000000004"),
                column: "Rate",
                value: null);

            migrationBuilder.UpdateData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("60000000-0000-0000-0000-000000000001"),
                column: "Rate",
                value: null);

            migrationBuilder.UpdateData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("60000000-0000-0000-0000-000000000002"),
                column: "Rate",
                value: null);

            migrationBuilder.UpdateData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("60000000-0000-0000-0000-000000000003"),
                column: "Rate",
                value: null);

            migrationBuilder.UpdateData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("70000000-0000-0000-0000-000000000001"),
                column: "Rate",
                value: null);

            migrationBuilder.UpdateData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("70000000-0000-0000-0000-000000000002"),
                column: "Rate",
                value: null);

            migrationBuilder.InsertData(
                table: "DataicoTaxCatalogItems",
                columns: new[] { "Id", "Category", "CreatedAt", "IsActive", "Kind", "Name", "Rate" },
                values: new object[,]
                {
                    { new Guid("11000000-0000-0000-0000-000000000001"), "RET_FUENTE", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, 0, "Compras generales (2.5%)", 2.5m },
                    { new Guid("11000000-0000-0000-0000-000000000002"), "RET_FUENTE", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, 0, "Servicios generales (4%)", 4m },
                    { new Guid("11000000-0000-0000-0000-000000000003"), "RET_FUENTE", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, 0, "Servicios profesionales / honorarios (11%)", 11m },
                    { new Guid("11000000-0000-0000-0000-000000000004"), "RET_ICA", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, 0, "Actividad industrial Bogotá (0.414%)", 0.414m },
                    { new Guid("11000000-0000-0000-0000-000000000005"), "RET_ICA", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, 0, "Actividad de servicios Bogotá (0.966%)", 0.966m },
                    { new Guid("11000000-0000-0000-0000-000000000006"), "RET_IVA", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, 0, "Estándar (15%)", 15m },
                    { new Guid("11000000-0000-0000-0000-000000000007"), "RET_IVA", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, 0, "Grandes contribuyentes (20%)", 20m }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("11000000-0000-0000-0000-000000000001"));

            migrationBuilder.DeleteData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("11000000-0000-0000-0000-000000000002"));

            migrationBuilder.DeleteData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("11000000-0000-0000-0000-000000000003"));

            migrationBuilder.DeleteData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("11000000-0000-0000-0000-000000000004"));

            migrationBuilder.DeleteData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("11000000-0000-0000-0000-000000000005"));

            migrationBuilder.DeleteData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("11000000-0000-0000-0000-000000000006"));

            migrationBuilder.DeleteData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("11000000-0000-0000-0000-000000000007"));

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

            migrationBuilder.DropColumn(
                name: "Rate",
                table: "DataicoTaxCatalogItems");

            migrationBuilder.UpdateData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000001"),
                column: "IsActive",
                value: true);

            migrationBuilder.UpdateData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000002"),
                column: "IsActive",
                value: true);

            migrationBuilder.UpdateData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000003"),
                column: "IsActive",
                value: true);
        }
    }
}
