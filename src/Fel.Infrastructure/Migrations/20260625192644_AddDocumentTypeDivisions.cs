using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Fel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentTypeDivisions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CustomizationId",
                table: "DocumentTypes",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DianCode",
                table: "DocumentTypes",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "OperationType",
                table: "DocumentTypes",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.InsertData(
                table: "DocumentTypes",
                columns: new[] { "Id", "Code", "CustomizationId", "Description", "DianCode", "IsActive", "Name", "OperationType" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0000-000000000001"), "FE-STD", null, "Factura Electrónica de Venta", "01", true, "Factura de Venta - Estándar", "10" },
                    { new Guid("00000000-0000-0000-0000-000000000002"), "FE-SALUD", null, "Factura Electrónica con RIPS", "01", true, "Factura de Venta - Sector Salud", "10" },
                    { new Guid("00000000-0000-0000-0000-000000000003"), "FE-AIU", null, "Servicios AIU", "01", true, "Factura de Venta - AIU", "09" },
                    { new Guid("00000000-0000-0000-0000-000000000004"), "FE-MANDATO", null, "Factura bajo Mandato", "01", true, "Factura de Venta - Mandatos", "11" },
                    { new Guid("00000000-0000-0000-0000-000000000005"), "FE-TRANSP", null, "Servicio de Transporte de Carga", "01", true, "Factura de Venta - Transporte", "15" },
                    { new Guid("00000000-0000-0000-0000-000000000006"), "FE-EXP", null, "Factura de Exportación", "02", true, "Factura de Venta - Exportación", "10" },
                    { new Guid("00000000-0000-0000-0000-000000000007"), "FC-FACT", null, "Contingencia del obligado a facturar", "03", true, "Factura de Contingencia Facturador", "10" },
                    { new Guid("00000000-0000-0000-0000-000000000008"), "FC-DIAN", null, "Contingencia tipo DIAN", "04", true, "Factura de Contingencia DIAN", "10" },
                    { new Guid("00000000-0000-0000-0000-000000000009"), "NC", null, "Nota Crédito Electrónica", "91", true, "Nota Crédito", "20" },
                    { new Guid("00000000-0000-0000-0000-000000000010"), "ND", null, "Nota Débito Electrónica", "92", true, "Nota Débito", "30" },
                    { new Guid("00000000-0000-0000-0000-000000000011"), "DE-POS", null, "Tiquete de máquina registradora POS", "20", true, "Doc. Equivalente - Tiquete POS", null },
                    { new Guid("00000000-0000-0000-0000-000000000012"), "DE-CINE", null, "Boleta de ingreso a cine", "06", true, "Doc. Equivalente - Cine", null },
                    { new Guid("00000000-0000-0000-0000-000000000013"), "DE-PASAJEROS", null, "Tiquete de transporte de pasajeros", "07", true, "Doc. Equivalente - Transporte Pasajeros", null },
                    { new Guid("00000000-0000-0000-0000-000000000014"), "DE-EXTRACTO", null, "Extracto expedido por sociedades", "08", true, "Doc. Equivalente - Extracto", null },
                    { new Guid("00000000-0000-0000-0000-000000000015"), "DE-AEREO", null, "Tiquete de transporte aéreo", "09", true, "Doc. Equivalente - Transporte Aéreo", null },
                    { new Guid("00000000-0000-0000-0000-000000000016"), "DE-JUEGOSLOC", null, "Documento en juegos localizados", "10", true, "Doc. Equivalente - Juegos Localizados", null },
                    { new Guid("00000000-0000-0000-0000-000000000017"), "DE-AZAR", null, "Boletas en juegos de suerte y azar", "11", true, "Doc. Equivalente - Suerte y Azar", null },
                    { new Guid("00000000-0000-0000-0000-000000000018"), "DE-PEAJE", null, "Cobro de peajes", "12", true, "Doc. Equivalente - Peajes", null },
                    { new Guid("00000000-0000-0000-0000-000000000019"), "DE-BOLSA", null, "Operaciones Bolsa de Valores", "13", true, "Doc. Equivalente - Bolsa de Valores", null },
                    { new Guid("00000000-0000-0000-0000-000000000020"), "DE-AGRO", null, "Operaciones Bolsa Agropecuaria", "14", true, "Doc. Equivalente - Bolsa Agropecuaria", null },
                    { new Guid("00000000-0000-0000-0000-000000000021"), "DE-SERVICIOSP", null, "Servicios públicos domiciliarios", "15", true, "Doc. Equivalente - Servicios Públicos", null },
                    { new Guid("00000000-0000-0000-0000-000000000022"), "DE-ESPECTACULOS", null, "Ingreso a espectáculos públicos", "16", true, "Doc. Equivalente - Espectáculos Públicos", null },
                    { new Guid("00000000-0000-0000-0000-000000000023"), "DE-AJUSTE", null, "Nota de ajuste para documentos equivalentes", "94", true, "Nota de Ajuste - Doc. Equivalente", null },
                    { new Guid("00000000-0000-0000-0000-000000000024"), "DS", null, "Documento soporte", "05", true, "Doc. Soporte - Adquisiciones a No Obligados", null },
                    { new Guid("00000000-0000-0000-0000-000000000025"), "DS-AJUSTE", null, "Ajuste a documento soporte", "95", true, "Nota de Ajuste - Doc. Soporte", null },
                    { new Guid("00000000-0000-0000-0000-000000000026"), "NE-PAGO", null, "Pago de nómina electrónica", "102", true, "Nómina Electrónica", null },
                    { new Guid("00000000-0000-0000-0000-000000000027"), "NE-AJUSTE", null, "Ajuste de nómina electrónica", "103", true, "Nota de Ajuste - Nómina Electrónica", null }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "DocumentTypes",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"));

            migrationBuilder.DeleteData(
                table: "DocumentTypes",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000002"));

            migrationBuilder.DeleteData(
                table: "DocumentTypes",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000003"));

            migrationBuilder.DeleteData(
                table: "DocumentTypes",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000004"));

            migrationBuilder.DeleteData(
                table: "DocumentTypes",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000005"));

            migrationBuilder.DeleteData(
                table: "DocumentTypes",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000006"));

            migrationBuilder.DeleteData(
                table: "DocumentTypes",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000007"));

            migrationBuilder.DeleteData(
                table: "DocumentTypes",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000008"));

            migrationBuilder.DeleteData(
                table: "DocumentTypes",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000009"));

            migrationBuilder.DeleteData(
                table: "DocumentTypes",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000010"));

            migrationBuilder.DeleteData(
                table: "DocumentTypes",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000011"));

            migrationBuilder.DeleteData(
                table: "DocumentTypes",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000012"));

            migrationBuilder.DeleteData(
                table: "DocumentTypes",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000013"));

            migrationBuilder.DeleteData(
                table: "DocumentTypes",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000014"));

            migrationBuilder.DeleteData(
                table: "DocumentTypes",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000015"));

            migrationBuilder.DeleteData(
                table: "DocumentTypes",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000016"));

            migrationBuilder.DeleteData(
                table: "DocumentTypes",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000017"));

            migrationBuilder.DeleteData(
                table: "DocumentTypes",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000018"));

            migrationBuilder.DeleteData(
                table: "DocumentTypes",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000019"));

            migrationBuilder.DeleteData(
                table: "DocumentTypes",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000020"));

            migrationBuilder.DeleteData(
                table: "DocumentTypes",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000021"));

            migrationBuilder.DeleteData(
                table: "DocumentTypes",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000022"));

            migrationBuilder.DeleteData(
                table: "DocumentTypes",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000023"));

            migrationBuilder.DeleteData(
                table: "DocumentTypes",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000024"));

            migrationBuilder.DeleteData(
                table: "DocumentTypes",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000025"));

            migrationBuilder.DeleteData(
                table: "DocumentTypes",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000026"));

            migrationBuilder.DeleteData(
                table: "DocumentTypes",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000027"));

            migrationBuilder.DropColumn(
                name: "CustomizationId",
                table: "DocumentTypes");

            migrationBuilder.DropColumn(
                name: "DianCode",
                table: "DocumentTypes");

            migrationBuilder.DropColumn(
                name: "OperationType",
                table: "DocumentTypes");
        }
    }
}
