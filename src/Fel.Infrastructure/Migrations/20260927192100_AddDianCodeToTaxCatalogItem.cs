using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDianCodeToTaxCatalogItem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DianCode",
                table: "TaxCatalogItems",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.UpdateData(
                table: "TaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000001"),
                column: "DianCode",
                value: null);

            migrationBuilder.UpdateData(
                table: "TaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000002"),
                column: "DianCode",
                value: null);

            migrationBuilder.UpdateData(
                table: "TaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000003"),
                column: "DianCode",
                value: null);

            migrationBuilder.UpdateData(
                table: "TaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000004"),
                column: "DianCode",
                value: null);

            migrationBuilder.UpdateData(
                table: "TaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000005"),
                column: "DianCode",
                value: null);

            migrationBuilder.UpdateData(
                table: "TaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000006"),
                column: "DianCode",
                value: null);

            migrationBuilder.UpdateData(
                table: "TaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000007"),
                column: "DianCode",
                value: null);

            migrationBuilder.UpdateData(
                table: "TaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000008"),
                column: "DianCode",
                value: null);

            migrationBuilder.UpdateData(
                table: "TaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000009"),
                column: "DianCode",
                value: null);

            migrationBuilder.UpdateData(
                table: "TaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-00000000000a"),
                column: "DianCode",
                value: null);

            migrationBuilder.UpdateData(
                table: "TaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-00000000000b"),
                column: "DianCode",
                value: null);

            migrationBuilder.UpdateData(
                table: "TaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-00000000000c"),
                column: "DianCode",
                value: null);

            migrationBuilder.UpdateData(
                table: "TaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-00000000000d"),
                column: "DianCode",
                value: null);

            migrationBuilder.UpdateData(
                table: "TaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-00000000000e"),
                column: "DianCode",
                value: null);

            migrationBuilder.UpdateData(
                table: "TaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-00000000000f"),
                column: "DianCode",
                value: "10");

            migrationBuilder.UpdateData(
                table: "TaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000010"),
                column: "DianCode",
                value: "45");

            migrationBuilder.UpdateData(
                table: "TaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000011"),
                column: "DianCode",
                value: "49");

            migrationBuilder.UpdateData(
                table: "TaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000012"),
                column: "DianCode",
                value: "48");

            migrationBuilder.UpdateData(
                table: "TaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000013"),
                column: "DianCode",
                value: null);

            migrationBuilder.UpdateData(
                table: "TaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000014"),
                column: "DianCode",
                value: null);

            migrationBuilder.UpdateData(
                table: "TaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("11000000-0000-0000-0000-000000000001"),
                column: "DianCode",
                value: null);

            migrationBuilder.UpdateData(
                table: "TaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("11000000-0000-0000-0000-000000000002"),
                column: "DianCode",
                value: null);

            migrationBuilder.UpdateData(
                table: "TaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("11000000-0000-0000-0000-000000000003"),
                column: "DianCode",
                value: null);

            migrationBuilder.UpdateData(
                table: "TaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("11000000-0000-0000-0000-000000000004"),
                column: "DianCode",
                value: null);

            migrationBuilder.UpdateData(
                table: "TaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("11000000-0000-0000-0000-000000000005"),
                column: "DianCode",
                value: null);

            migrationBuilder.UpdateData(
                table: "TaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("11000000-0000-0000-0000-000000000006"),
                column: "DianCode",
                value: null);

            migrationBuilder.UpdateData(
                table: "TaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("11000000-0000-0000-0000-000000000007"),
                column: "DianCode",
                value: null);

            migrationBuilder.UpdateData(
                table: "TaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000001"),
                column: "DianCode",
                value: null);

            migrationBuilder.UpdateData(
                table: "TaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000002"),
                column: "DianCode",
                value: null);

            migrationBuilder.UpdateData(
                table: "TaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000003"),
                column: "DianCode",
                value: null);

            migrationBuilder.UpdateData(
                table: "TaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000004"),
                column: "DianCode",
                value: null);

            migrationBuilder.UpdateData(
                table: "TaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000005"),
                column: "DianCode",
                value: null);

            migrationBuilder.UpdateData(
                table: "TaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000006"),
                column: "DianCode",
                value: null);

            migrationBuilder.UpdateData(
                table: "TaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000007"),
                column: "DianCode",
                value: null);

            migrationBuilder.UpdateData(
                table: "TaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000008"),
                column: "DianCode",
                value: null);

            migrationBuilder.UpdateData(
                table: "TaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000009"),
                column: "DianCode",
                value: null);

            migrationBuilder.UpdateData(
                table: "TaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000010"),
                column: "DianCode",
                value: null);

            migrationBuilder.UpdateData(
                table: "TaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000011"),
                column: "DianCode",
                value: null);

            migrationBuilder.UpdateData(
                table: "TaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000012"),
                column: "DianCode",
                value: null);

            migrationBuilder.UpdateData(
                table: "TaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000013"),
                column: "DianCode",
                value: null);

            migrationBuilder.UpdateData(
                table: "TaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000014"),
                column: "DianCode",
                value: null);

            migrationBuilder.UpdateData(
                table: "TaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000015"),
                column: "DianCode",
                value: null);

            migrationBuilder.UpdateData(
                table: "TaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000016"),
                column: "DianCode",
                value: null);

            migrationBuilder.UpdateData(
                table: "TaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000017"),
                column: "DianCode",
                value: null);

            migrationBuilder.UpdateData(
                table: "TaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000018"),
                column: "DianCode",
                value: null);

            migrationBuilder.UpdateData(
                table: "TaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000001"),
                column: "DianCode",
                value: null);

            migrationBuilder.UpdateData(
                table: "TaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000002"),
                column: "DianCode",
                value: null);

            migrationBuilder.UpdateData(
                table: "TaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000003"),
                column: "DianCode",
                value: null);

            migrationBuilder.UpdateData(
                table: "TaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000004"),
                column: "DianCode",
                value: null);

            migrationBuilder.UpdateData(
                table: "TaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000005"),
                column: "DianCode",
                value: null);

            migrationBuilder.UpdateData(
                table: "TaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000001"),
                column: "DianCode",
                value: null);

            migrationBuilder.UpdateData(
                table: "TaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000002"),
                column: "DianCode",
                value: null);

            migrationBuilder.UpdateData(
                table: "TaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000003"),
                column: "DianCode",
                value: null);

            migrationBuilder.UpdateData(
                table: "TaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000004"),
                column: "DianCode",
                value: null);

            migrationBuilder.UpdateData(
                table: "TaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000005"),
                column: "DianCode",
                value: null);

            migrationBuilder.UpdateData(
                table: "TaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000006"),
                column: "DianCode",
                value: null);

            migrationBuilder.UpdateData(
                table: "TaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000007"),
                column: "DianCode",
                value: null);

            migrationBuilder.UpdateData(
                table: "TaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000008"),
                column: "DianCode",
                value: null);

            migrationBuilder.UpdateData(
                table: "TaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000009"),
                column: "DianCode",
                value: null);

            migrationBuilder.UpdateData(
                table: "TaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000010"),
                column: "DianCode",
                value: null);

            migrationBuilder.UpdateData(
                table: "TaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000011"),
                column: "DianCode",
                value: null);

            migrationBuilder.UpdateData(
                table: "TaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000012"),
                column: "DianCode",
                value: null);

            migrationBuilder.UpdateData(
                table: "TaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000013"),
                column: "DianCode",
                value: null);

            migrationBuilder.UpdateData(
                table: "TaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000014"),
                column: "DianCode",
                value: null);

            migrationBuilder.UpdateData(
                table: "TaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000015"),
                column: "DianCode",
                value: null);

            migrationBuilder.UpdateData(
                table: "TaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000016"),
                column: "DianCode",
                value: null);

            migrationBuilder.UpdateData(
                table: "TaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000017"),
                column: "DianCode",
                value: null);

            migrationBuilder.UpdateData(
                table: "TaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000018"),
                column: "DianCode",
                value: null);

            migrationBuilder.UpdateData(
                table: "TaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000019"),
                column: "DianCode",
                value: null);

            migrationBuilder.UpdateData(
                table: "TaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000020"),
                column: "DianCode",
                value: null);

            migrationBuilder.UpdateData(
                table: "TaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000021"),
                column: "DianCode",
                value: null);

            migrationBuilder.UpdateData(
                table: "TaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000022"),
                column: "DianCode",
                value: null);

            migrationBuilder.UpdateData(
                table: "TaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000023"),
                column: "DianCode",
                value: null);

            migrationBuilder.UpdateData(
                table: "TaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000024"),
                column: "DianCode",
                value: null);

            migrationBuilder.UpdateData(
                table: "TaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000025"),
                column: "DianCode",
                value: null);

            migrationBuilder.UpdateData(
                table: "TaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000026"),
                column: "DianCode",
                value: null);

            migrationBuilder.UpdateData(
                table: "TaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000027"),
                column: "DianCode",
                value: null);

            migrationBuilder.UpdateData(
                table: "TaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000028"),
                column: "DianCode",
                value: null);

            migrationBuilder.UpdateData(
                table: "TaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000029"),
                column: "DianCode",
                value: null);

            migrationBuilder.UpdateData(
                table: "TaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000030"),
                column: "DianCode",
                value: null);

            migrationBuilder.UpdateData(
                table: "TaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000031"),
                column: "DianCode",
                value: null);

            migrationBuilder.UpdateData(
                table: "TaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000032"),
                column: "DianCode",
                value: null);

            migrationBuilder.UpdateData(
                table: "TaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000033"),
                column: "DianCode",
                value: null);

            migrationBuilder.UpdateData(
                table: "TaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000034"),
                column: "DianCode",
                value: null);

            migrationBuilder.UpdateData(
                table: "TaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000035"),
                column: "DianCode",
                value: null);

            migrationBuilder.UpdateData(
                table: "TaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000036"),
                column: "DianCode",
                value: null);

            migrationBuilder.UpdateData(
                table: "TaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000037"),
                column: "DianCode",
                value: null);

            migrationBuilder.UpdateData(
                table: "TaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000038"),
                column: "DianCode",
                value: null);

            migrationBuilder.UpdateData(
                table: "TaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000039"),
                column: "DianCode",
                value: null);

            migrationBuilder.UpdateData(
                table: "TaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000040"),
                column: "DianCode",
                value: null);

            migrationBuilder.UpdateData(
                table: "TaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000041"),
                column: "DianCode",
                value: null);

            migrationBuilder.UpdateData(
                table: "TaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000042"),
                column: "DianCode",
                value: null);

            migrationBuilder.UpdateData(
                table: "TaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000043"),
                column: "DianCode",
                value: null);

            migrationBuilder.UpdateData(
                table: "TaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000044"),
                column: "DianCode",
                value: null);

            migrationBuilder.UpdateData(
                table: "TaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000045"),
                column: "DianCode",
                value: null);

            migrationBuilder.UpdateData(
                table: "TaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000046"),
                column: "DianCode",
                value: null);

            migrationBuilder.UpdateData(
                table: "TaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000047"),
                column: "DianCode",
                value: null);

            migrationBuilder.UpdateData(
                table: "TaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000048"),
                column: "DianCode",
                value: null);

            migrationBuilder.UpdateData(
                table: "TaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000049"),
                column: "DianCode",
                value: null);

            migrationBuilder.UpdateData(
                table: "TaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000050"),
                column: "DianCode",
                value: null);

            migrationBuilder.UpdateData(
                table: "TaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000051"),
                column: "DianCode",
                value: null);

            migrationBuilder.UpdateData(
                table: "TaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000052"),
                column: "DianCode",
                value: null);

            migrationBuilder.UpdateData(
                table: "TaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000053"),
                column: "DianCode",
                value: null);

            migrationBuilder.UpdateData(
                table: "TaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("50000000-0000-0000-0000-000000000001"),
                column: "DianCode",
                value: null);

            migrationBuilder.UpdateData(
                table: "TaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("50000000-0000-0000-0000-000000000002"),
                column: "DianCode",
                value: null);

            migrationBuilder.UpdateData(
                table: "TaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("50000000-0000-0000-0000-000000000003"),
                column: "DianCode",
                value: null);

            migrationBuilder.UpdateData(
                table: "TaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("50000000-0000-0000-0000-000000000004"),
                column: "DianCode",
                value: null);

            migrationBuilder.UpdateData(
                table: "TaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("60000000-0000-0000-0000-000000000001"),
                column: "DianCode",
                value: null);

            migrationBuilder.UpdateData(
                table: "TaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("60000000-0000-0000-0000-000000000002"),
                column: "DianCode",
                value: null);

            migrationBuilder.UpdateData(
                table: "TaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("60000000-0000-0000-0000-000000000003"),
                column: "DianCode",
                value: null);

            migrationBuilder.UpdateData(
                table: "TaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("70000000-0000-0000-0000-000000000001"),
                column: "DianCode",
                value: null);

            migrationBuilder.UpdateData(
                table: "TaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("70000000-0000-0000-0000-000000000002"),
                column: "DianCode",
                value: null);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DianCode",
                table: "TaxCatalogItems");
        }
    }
}
