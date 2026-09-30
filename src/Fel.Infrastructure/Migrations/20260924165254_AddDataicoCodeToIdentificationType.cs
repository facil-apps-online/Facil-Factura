using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDataicoCodeToIdentificationType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DataicoCode",
                table: "IdentificationTypes",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "IdentificationTypes",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000001"),
                column: "DataicoCode",
                value: "CC");

            migrationBuilder.UpdateData(
                table: "IdentificationTypes",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000002"),
                column: "DataicoCode",
                value: "CE");

            migrationBuilder.UpdateData(
                table: "IdentificationTypes",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000003"),
                column: "DataicoCode",
                value: "IE");

            migrationBuilder.UpdateData(
                table: "IdentificationTypes",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000004"),
                column: "DataicoCode",
                value: "NIT");

            migrationBuilder.UpdateData(
                table: "IdentificationTypes",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000005"),
                column: "DataicoCode",
                value: "NIT_OTRO_PAIS");

            migrationBuilder.UpdateData(
                table: "IdentificationTypes",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000006"),
                column: "DataicoCode",
                value: "NUIP");

            migrationBuilder.UpdateData(
                table: "IdentificationTypes",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000007"),
                column: "DataicoCode",
                value: "PASAPORTE");

            migrationBuilder.UpdateData(
                table: "IdentificationTypes",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000008"),
                column: "DataicoCode",
                value: "RC");

            migrationBuilder.UpdateData(
                table: "IdentificationTypes",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000009"),
                column: "DataicoCode",
                value: "TE");

            migrationBuilder.UpdateData(
                table: "IdentificationTypes",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-00000000000a"),
                column: "DataicoCode",
                value: "TI");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DataicoCode",
                table: "IdentificationTypes");
        }
    }
}
