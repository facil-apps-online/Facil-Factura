using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixDocumentEquivalenteDianCodes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "DocumentTypes",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000012"),
                column: "DianCode",
                value: "25");

            migrationBuilder.UpdateData(
                table: "DocumentTypes",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000013"),
                columns: new[] { "Description", "DianCode" },
                values: new object[] { "Tiquete de transporte terrestre de pasajeros", "35" });

            migrationBuilder.UpdateData(
                table: "DocumentTypes",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000014"),
                columns: new[] { "Description", "DianCode" },
                values: new object[] { "Extracto expedido por sociedades financieras y fondos", "45" });

            migrationBuilder.UpdateData(
                table: "DocumentTypes",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000015"),
                columns: new[] { "Description", "DianCode" },
                values: new object[] { "Tiquete de transporte aéreo de pasajeros", "50" });

            migrationBuilder.UpdateData(
                table: "DocumentTypes",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000016"),
                columns: new[] { "Description", "DianCode" },
                values: new object[] { "Documento en juegos localizados y no localizados", "30" });

            migrationBuilder.UpdateData(
                table: "DocumentTypes",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000017"),
                columns: new[] { "Description", "DianCode" },
                values: new object[] { "Boletas en juegos de suerte y azar (mismo código DIAN que juegos localizados)", "30" });

            migrationBuilder.UpdateData(
                table: "DocumentTypes",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000018"),
                column: "DianCode",
                value: "40");

            migrationBuilder.UpdateData(
                table: "DocumentTypes",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000019"),
                columns: new[] { "Description", "DianCode" },
                values: new object[] { "Liquidación de operaciones Bolsa de Valores", "55" });

            migrationBuilder.UpdateData(
                table: "DocumentTypes",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000020"),
                columns: new[] { "Description", "DianCode" },
                values: new object[] { "Operaciones bolsa agropecuaria y otros commodities (mismo código DIAN que bolsa de valores)", "55" });

            migrationBuilder.UpdateData(
                table: "DocumentTypes",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000021"),
                column: "DianCode",
                value: "60");

            migrationBuilder.UpdateData(
                table: "DocumentTypes",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000022"),
                column: "DianCode",
                value: "27");

            migrationBuilder.UpdateData(
                table: "DocumentTypes",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000023"),
                columns: new[] { "Code", "Description", "Name" },
                values: new object[] { "DE-AJUSTE-CREDITO", "Nota de ajuste tipo crédito para documentos equivalentes", "Nota de Ajuste (Crédito) - Doc. Equivalente" });

            migrationBuilder.InsertData(
                table: "DocumentTypes",
                columns: new[] { "Id", "Code", "CustomizationId", "Description", "DianCode", "GoverningEntity", "IsActive", "Name", "OperationType" },
                values: new object[] { new Guid("00000000-0000-0000-0000-000000000032"), "DE-AJUSTE-DEBITO", null, "Nota de ajuste tipo débito para documentos equivalentes", "93", "DIAN", true, "Nota de Ajuste (Débito) - Doc. Equivalente", null });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "DocumentTypes",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000032"));

            migrationBuilder.UpdateData(
                table: "DocumentTypes",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000012"),
                column: "DianCode",
                value: "06");

            migrationBuilder.UpdateData(
                table: "DocumentTypes",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000013"),
                columns: new[] { "Description", "DianCode" },
                values: new object[] { "Tiquete de transporte de pasajeros", "07" });

            migrationBuilder.UpdateData(
                table: "DocumentTypes",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000014"),
                columns: new[] { "Description", "DianCode" },
                values: new object[] { "Extracto expedido por sociedades", "08" });

            migrationBuilder.UpdateData(
                table: "DocumentTypes",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000015"),
                columns: new[] { "Description", "DianCode" },
                values: new object[] { "Tiquete de transporte aéreo", "09" });

            migrationBuilder.UpdateData(
                table: "DocumentTypes",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000016"),
                columns: new[] { "Description", "DianCode" },
                values: new object[] { "Documento en juegos localizados", "10" });

            migrationBuilder.UpdateData(
                table: "DocumentTypes",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000017"),
                columns: new[] { "Description", "DianCode" },
                values: new object[] { "Boletas en juegos de suerte y azar", "11" });

            migrationBuilder.UpdateData(
                table: "DocumentTypes",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000018"),
                column: "DianCode",
                value: "12");

            migrationBuilder.UpdateData(
                table: "DocumentTypes",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000019"),
                columns: new[] { "Description", "DianCode" },
                values: new object[] { "Operaciones Bolsa de Valores", "13" });

            migrationBuilder.UpdateData(
                table: "DocumentTypes",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000020"),
                columns: new[] { "Description", "DianCode" },
                values: new object[] { "Operaciones Bolsa Agropecuaria", "14" });

            migrationBuilder.UpdateData(
                table: "DocumentTypes",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000021"),
                column: "DianCode",
                value: "15");

            migrationBuilder.UpdateData(
                table: "DocumentTypes",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000022"),
                column: "DianCode",
                value: "16");

            migrationBuilder.UpdateData(
                table: "DocumentTypes",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000023"),
                columns: new[] { "Code", "Description", "Name" },
                values: new object[] { "DE-AJUSTE", "Nota de ajuste para documentos equivalentes", "Nota de Ajuste - Doc. Equivalente" });
        }
    }
}
