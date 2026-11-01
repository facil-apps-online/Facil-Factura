using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Fel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPayrollConceptTypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PayrollConceptTypes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Section = table.Column<int>(type: "int", nullable: false),
                    DataicoCode = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    DianElement = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    DianShape = table.Column<int>(type: "int", nullable: false),
                    DianAmountAttribute = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    DianNonSalaryAttribute = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    DianQuantityAttribute = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    DianPercentageAttribute = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    QuantityKind = table.Column<int>(type: "int", nullable: false),
                    HasPercentage = table.Column<bool>(type: "bit", nullable: false),
                    AllowsAmount = table.Column<bool>(type: "bit", nullable: false),
                    AllowsNonSalary = table.Column<bool>(type: "bit", nullable: false),
                    RequiresDescription = table.Column<bool>(type: "bit", nullable: false),
                    SpecialInput = table.Column<int>(type: "int", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayrollConceptTypes", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "PayrollConceptTypes",
                columns: new[] { "Id", "AllowsAmount", "AllowsNonSalary", "DataicoCode", "DianAmountAttribute", "DianElement", "DianNonSalaryAttribute", "DianPercentageAttribute", "DianQuantityAttribute", "DianShape", "HasPercentage", "IsActive", "Name", "QuantityKind", "RequiresDescription", "Section", "SortOrder", "SpecialInput" },
                values: new object[,]
                {
                    { new Guid("b0000000-0000-0000-0000-000000000001"), true, false, "BASICO", "SueldoTrabajado", "Basico", null, null, "DiasTrabajados", 0, false, true, "Salario básico", 1, false, 0, 1, 0 },
                    { new Guid("b0000000-0000-0000-0000-000000000002"), true, false, "AUXILIO_DE_TRANSPORTE", "AuxilioTransporte", "Transporte", null, null, null, 0, false, true, "Auxilio de transporte", 0, false, 0, 2, 0 },
                    { new Guid("b0000000-0000-0000-0000-000000000003"), true, false, "HORA_EXTRA_DIURNA", "Pago", "HED", null, "Porcentaje", "Cantidad", 1, true, true, "Hora extra diurna", 2, false, 0, 3, 0 },
                    { new Guid("b0000000-0000-0000-0000-000000000004"), true, false, "HORA_EXTRA_NOCTURNA", "Pago", "HEN", null, "Porcentaje", "Cantidad", 1, true, true, "Hora extra nocturna", 2, false, 0, 4, 0 },
                    { new Guid("b0000000-0000-0000-0000-000000000005"), true, false, "HORA_EXTRA_DIURNA_DF", "Pago", "HEDDF", null, "Porcentaje", "Cantidad", 1, true, true, "Hora extra diurna dominical/festiva", 2, false, 0, 5, 0 },
                    { new Guid("b0000000-0000-0000-0000-000000000006"), true, false, "HORA_EXTRA_NOCTURNA_DF", "Pago", "HENDF", null, "Porcentaje", "Cantidad", 1, true, true, "Hora extra nocturna dominical/festiva", 2, false, 0, 6, 0 },
                    { new Guid("b0000000-0000-0000-0000-000000000007"), true, false, "HORA_RECARGO_NOCTURNO", "Pago", "HRN", null, "Porcentaje", "Cantidad", 1, true, true, "Recargo nocturno", 2, false, 0, 7, 0 },
                    { new Guid("b0000000-0000-0000-0000-000000000008"), true, false, "HORA_RECARGO_DIURNA_DF", "Pago", "HRDDF", null, "Porcentaje", "Cantidad", 1, true, true, "Recargo diurno dominical/festivo", 2, false, 0, 8, 0 },
                    { new Guid("b0000000-0000-0000-0000-000000000009"), true, false, "HORA_RECARGO_NOCTURNO_DF", "Pago", "HRNDF", null, "Porcentaje", "Cantidad", 1, true, true, "Recargo nocturno dominical/festivo", 2, false, 0, 9, 0 },
                    { new Guid("b0000000-0000-0000-0000-000000000010"), true, false, "VACACION", "Pago", "VacacionesComunes", null, null, "Cantidad", 1, false, true, "Vacaciones disfrutadas", 1, false, 0, 10, 0 },
                    { new Guid("b0000000-0000-0000-0000-000000000011"), true, false, "VACACION_COMPENSADA", "Pago", "VacacionesCompensadas", null, null, "Cantidad", 0, false, true, "Vacaciones compensadas", 1, false, 0, 11, 0 },
                    { new Guid("b0000000-0000-0000-0000-000000000012"), true, true, "PRIMA", "Pago", "Primas", "PagoNS", null, "Cantidad", 0, false, true, "Prima de servicios", 1, false, 0, 12, 0 },
                    { new Guid("b0000000-0000-0000-0000-000000000013"), true, true, "CESANTIAS", "Pago", "Cesantias", null, "Porcentaje", null, 0, false, true, "Cesantías", 3, false, 0, 13, 3 },
                    { new Guid("b0000000-0000-0000-0000-000000000014"), true, false, "INCAPACIDAD", "Pago", "Incapacidad", null, null, "Cantidad", 1, false, true, "Incapacidad", 1, false, 0, 14, 2 },
                    { new Guid("b0000000-0000-0000-0000-000000000015"), true, false, "LICENCIA_PATERNIDAD", "Pago", "LicenciaMP", null, null, "Cantidad", 1, false, true, "Licencia de maternidad o paternidad", 1, false, 0, 15, 1 },
                    { new Guid("b0000000-0000-0000-0000-000000000016"), true, false, "LICENCIA_REMUNERADA", "Pago", "LicenciaR", null, null, "Cantidad", 1, false, true, "Licencia remunerada", 1, false, 0, 16, 0 },
                    { new Guid("b0000000-0000-0000-0000-000000000017"), false, false, "LICENCIA_NO_REMUNERADA", null, "LicenciaNR", null, null, "Cantidad", 1, false, true, "Licencia no remunerada", 1, false, 0, 17, 0 },
                    { new Guid("b0000000-0000-0000-0000-000000000018"), false, false, "HUELGA_LEGAL", null, "HuelgaLegal", null, null, "Cantidad", 1, false, true, "Huelga legal", 1, false, 0, 18, 1 },
                    { new Guid("b0000000-0000-0000-0000-000000000019"), true, true, "BONIFICACION", "BonificacionS", "Bonificacion", "BonificacionNS", null, null, 0, false, true, "Bonificación", 0, false, 0, 19, 0 },
                    { new Guid("b0000000-0000-0000-0000-000000000020"), true, true, "AUXILIO", "AuxilioS", "Auxilio", "AuxilioNS", null, null, 0, false, true, "Auxilio", 0, false, 0, 20, 0 },
                    { new Guid("b0000000-0000-0000-0000-000000000021"), true, true, "VIATICO", "ViaticoManuAlojS", "Transporte", "ViaticoManuAlojNS", null, null, 0, false, true, "Viático", 0, false, 0, 21, 0 },
                    { new Guid("b0000000-0000-0000-0000-000000000022"), true, false, "COMISION", null, "Comision", null, null, null, 2, false, true, "Comisión", 0, false, 0, 22, 0 },
                    { new Guid("b0000000-0000-0000-0000-000000000023"), true, true, "BONO_EPCTV", "PagoS", "BonoEPCTV", "PagoNS", null, null, 0, false, true, "Bono electrónico o de papel", 0, false, 0, 23, 0 },
                    { new Guid("b0000000-0000-0000-0000-000000000024"), true, true, "BONO_EPCTV_ALIMENTACION", "PagoAlimentacionS", "BonoEPCTV", "PagoAlimentacionNS", null, null, 0, false, true, "Bono de alimentación", 0, false, 0, 24, 0 },
                    { new Guid("b0000000-0000-0000-0000-000000000025"), true, true, "DOTACION", null, "Dotacion", null, null, null, 2, false, true, "Dotación", 0, false, 0, 25, 0 },
                    { new Guid("b0000000-0000-0000-0000-000000000026"), true, true, "APOYO_PRACTICA", null, "ApoyoSost", null, null, null, 2, false, true, "Apoyo de sostenimiento", 0, false, 0, 26, 0 },
                    { new Guid("b0000000-0000-0000-0000-000000000027"), true, false, "TELETRABAJO", null, "Teletrabajo", null, null, null, 2, false, true, "Teletrabajo", 0, false, 0, 27, 0 },
                    { new Guid("b0000000-0000-0000-0000-000000000028"), true, true, "BONIFICACION_RETIRO", null, "BonifRetiro", null, null, null, 2, false, true, "Bonificación de retiro", 0, false, 0, 28, 0 },
                    { new Guid("b0000000-0000-0000-0000-000000000029"), true, true, "INDEMNIZACION", null, "Indemnizacion", null, null, null, 2, false, true, "Indemnización", 0, false, 0, 29, 0 },
                    { new Guid("b0000000-0000-0000-0000-000000000030"), true, false, "PAGO_TERCERO", null, "PagoTercero", null, null, null, 2, false, true, "Pago a terceros", 0, false, 0, 30, 0 },
                    { new Guid("b0000000-0000-0000-0000-000000000031"), true, false, "ANTICIPO", null, "Anticipo", null, null, null, 2, false, true, "Anticipo", 0, false, 0, 31, 0 },
                    { new Guid("b0000000-0000-0000-0000-000000000032"), true, false, "REINTEGRO", null, "Reintegro", null, null, null, 2, false, true, "Reintegro", 0, false, 0, 32, 0 },
                    { new Guid("b0000000-0000-0000-0000-000000000033"), false, false, "COMPENSACION", "CompensacionO", "Compensacion", "CompensacionE", null, null, 0, false, true, "Compensación", 0, false, 0, 33, 4 },
                    { new Guid("b0000000-0000-0000-0000-000000000034"), true, true, "OTRO_CONCEPTO", "ConceptoS", "OtroConcepto", "ConceptoNS", null, null, 1, false, true, "Otro concepto", 1, true, 0, 34, 0 },
                    { new Guid("b0000000-0000-0000-0000-000000000035"), true, false, "SALUD", "Deduccion", "Salud", null, "Porcentaje", null, 0, false, true, "Salud", 3, false, 1, 35, 0 },
                    { new Guid("b0000000-0000-0000-0000-000000000036"), true, false, "FONDO_PENSION", "Deduccion", "FondoPension", null, "Porcentaje", null, 0, false, true, "Fondo de pensión", 3, false, 1, 36, 0 },
                    { new Guid("b0000000-0000-0000-0000-000000000037"), true, false, "FONDO_SOLIDARIDAD_PENSIONAL", "DeduccionSP", "FondoSP", null, "Porcentaje", null, 0, false, true, "Fondo de solidaridad pensional", 3, false, 1, 37, 0 },
                    { new Guid("b0000000-0000-0000-0000-000000000038"), true, false, "FONDO_SUBSISTENCIA", "DeduccionSub", "FondoSP", null, "PorcentajeSub", null, 0, false, true, "Fondo de subsistencia", 3, false, 1, 38, 0 },
                    { new Guid("b0000000-0000-0000-0000-000000000039"), true, false, "SINDICATO", "Deduccion", "Sindicato", null, "Porcentaje", null, 1, false, true, "Sindicato", 3, false, 1, 39, 0 },
                    { new Guid("b0000000-0000-0000-0000-000000000040"), true, false, "RETENCION_FUENTE", null, "RetencionFuente", null, null, null, 2, false, true, "Retención en la fuente", 0, false, 1, 40, 0 },
                    { new Guid("b0000000-0000-0000-0000-000000000041"), true, false, "LIBRANZA", "Deduccion", "Libranza", null, null, null, 1, false, true, "Libranza", 0, true, 1, 41, 0 },
                    { new Guid("b0000000-0000-0000-0000-000000000042"), true, false, "PENSION_VOLUNTARIA", null, "PensionVoluntaria", null, null, null, 2, false, true, "Pensión voluntaria", 0, false, 1, 42, 0 },
                    { new Guid("b0000000-0000-0000-0000-000000000043"), true, false, "AFC", null, "AFC", null, null, null, 2, false, true, "Ahorro AFC", 0, false, 1, 43, 0 },
                    { new Guid("b0000000-0000-0000-0000-000000000044"), true, false, "COOPERATIVA", null, "Cooperativa", null, null, null, 2, false, true, "Cooperativa", 0, false, 1, 44, 0 },
                    { new Guid("b0000000-0000-0000-0000-000000000045"), true, false, "EMBARGO_FISCAL", null, "EmbargoFiscal", null, null, null, 2, false, true, "Embargo fiscal", 0, false, 1, 45, 0 },
                    { new Guid("b0000000-0000-0000-0000-000000000046"), true, false, "PLANES_COMPLEMENTARIOS", null, "PlanComplementarios", null, null, null, 2, false, true, "Planes complementarios", 0, false, 1, 46, 0 },
                    { new Guid("b0000000-0000-0000-0000-000000000047"), true, false, "EDUCACION", null, "Educacion", null, null, null, 2, false, true, "Educación", 0, false, 1, 47, 0 },
                    { new Guid("b0000000-0000-0000-0000-000000000048"), true, false, "REINTEGRO", null, "Reintegro", null, null, null, 2, false, true, "Reintegro", 0, false, 1, 48, 0 },
                    { new Guid("b0000000-0000-0000-0000-000000000049"), true, false, "DEUDA", null, "Deuda", null, null, null, 2, false, true, "Deuda", 0, false, 1, 49, 0 },
                    { new Guid("b0000000-0000-0000-0000-000000000050"), true, false, "PAGO_TERCERO", null, "PagoTercero", null, null, null, 2, false, true, "Pago a terceros", 0, false, 1, 50, 0 },
                    { new Guid("b0000000-0000-0000-0000-000000000051"), true, false, "ANTICIPO", null, "Anticipo", null, null, null, 2, false, true, "Anticipo", 0, false, 1, 51, 0 },
                    { new Guid("b0000000-0000-0000-0000-000000000052"), false, false, "SANCION", "SancionPublic", "Sancion", "SancionPriv", null, null, 1, false, true, "Sanción", 0, false, 1, 52, 5 },
                    { new Guid("b0000000-0000-0000-0000-000000000053"), true, false, "OTRA_DEDUCCION", null, "OtraDeduccion", null, null, null, 2, false, true, "Otra deducción", 0, true, 1, 53, 0 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_PayrollConceptTypes_Section_DataicoCode",
                table: "PayrollConceptTypes",
                columns: new[] { "Section", "DataicoCode" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PayrollConceptTypes");
        }
    }
}
