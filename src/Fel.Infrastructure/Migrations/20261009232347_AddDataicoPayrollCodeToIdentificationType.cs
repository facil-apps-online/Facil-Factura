using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDataicoPayrollCodeToIdentificationType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DataicoPayrollCode",
                table: "IdentificationTypes",
                type: "nvarchar(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.UpdateData(
                table: "IdentificationTypes",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000001"),
                column: "DataicoPayrollCode",
                value: "CEDULA_DE_CIUDADANIA");

            migrationBuilder.UpdateData(
                table: "IdentificationTypes",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000002"),
                column: "DataicoPayrollCode",
                value: "CEDULA_DE_EXTRANJERIA");

            migrationBuilder.UpdateData(
                table: "IdentificationTypes",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000003"),
                column: "DataicoPayrollCode",
                value: "DOCUMENTO_DE_IDENTIFICATION_EXTRANJERO");

            migrationBuilder.UpdateData(
                table: "IdentificationTypes",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000004"),
                column: "DataicoPayrollCode",
                value: "NIT");

            migrationBuilder.UpdateData(
                table: "IdentificationTypes",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000005"),
                column: "DataicoPayrollCode",
                value: "NIT_DE_OTRO_PAIS");

            migrationBuilder.UpdateData(
                table: "IdentificationTypes",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000006"),
                column: "DataicoPayrollCode",
                value: "NUIP");

            migrationBuilder.UpdateData(
                table: "IdentificationTypes",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000007"),
                column: "DataicoPayrollCode",
                value: "PASAPORTE");

            migrationBuilder.UpdateData(
                table: "IdentificationTypes",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000008"),
                column: "DataicoPayrollCode",
                value: "REGISTRO_CIVIL");

            migrationBuilder.UpdateData(
                table: "IdentificationTypes",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000009"),
                column: "DataicoPayrollCode",
                value: "TARJETA_DE_EXTRANJERIA");

            migrationBuilder.UpdateData(
                table: "IdentificationTypes",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-00000000000a"),
                column: "DataicoPayrollCode",
                value: "TARJETA_DE_IDENTIDAD");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DataicoPayrollCode",
                table: "IdentificationTypes");
        }
    }
}
