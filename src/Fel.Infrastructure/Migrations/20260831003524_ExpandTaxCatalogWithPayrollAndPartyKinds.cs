using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Fel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ExpandTaxCatalogWithPayrollAndPartyKinds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "DataicoTaxCatalogItems",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(150)",
                oldMaxLength: 150);

            migrationBuilder.AlterColumn<string>(
                name: "Category",
                table: "DataicoTaxCatalogItems",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(30)",
                oldMaxLength: 30);

            migrationBuilder.InsertData(
                table: "DataicoTaxCatalogItems",
                columns: new[] { "Id", "Category", "CreatedAt", "IsActive", "Kind", "Name" },
                values: new object[,]
                {
                    { new Guid("20000000-0000-0000-0000-000000000001"), "DEPENDIENTE", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, 5, "Dependiente" },
                    { new Guid("20000000-0000-0000-0000-000000000002"), "PROFESOR_DE_ESTABLECIMIENTO_PARTICULAR", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, 5, "Profesor De Establecimiento Particular" },
                    { new Guid("20000000-0000-0000-0000-000000000003"), "PRE_PENSIONADO_CON_APORTE_VOLUNTARIO_A_SALUD", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, 5, "Pre Pensionado Con Aporte Voluntario A Salud" },
                    { new Guid("20000000-0000-0000-0000-000000000004"), "SERVICIO_DOMESTICO", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, 5, "Servicio Domestico" },
                    { new Guid("20000000-0000-0000-0000-000000000005"), "APRENDICES_DEL_SENA_EN_ETAPA_LECTIVA", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, 5, "Aprendices Del Sena En Etapa Lectiva" },
                    { new Guid("20000000-0000-0000-0000-000000000006"), "COOPERADOS_O_PRE_COOPERATIVAS_DE_TRABAJO_ASOCIADO", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, 5, "Cooperados O Pre Cooperativas De Trabajo Asociado" },
                    { new Guid("20000000-0000-0000-0000-000000000007"), "ESTUDIANTES_DE_PRACTICAS_LABORALES_EN_EL_SECTOR_PUBLICO", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, 5, "Estudiantes De Practicas Laborales En El Sector Publico" },
                    { new Guid("20000000-0000-0000-0000-000000000008"), "TRABAJADOR_DEPENDIENTE_DE_ENTIDAD_BENEFICIARIA_DEL_SISTEMA_GENERAL_DE_PARTICIPACIONES_APORTES_PATRONALES", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, 5, "Trabajador Dependiente De Entidad Beneficiaria Del Sistema General De Participaciones Aportes Patronales" },
                    { new Guid("20000000-0000-0000-0000-000000000009"), "FUNCIONARIOS_PUBLICOS_SIN_TOPE_MAXIMO_DE_IBC", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, 5, "Funcionarios Publicos Sin Tope Maximo De Ibc" },
                    { new Guid("20000000-0000-0000-0000-000000000010"), "MADRE_COMUNITARIA", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, 5, "Madre Comunitaria" },
                    { new Guid("20000000-0000-0000-0000-000000000011"), "ESTUDIANTES_DE_POSTGRADO_EN_SALUD", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, 5, "Estudiantes De Postgrado En Salud" },
                    { new Guid("20000000-0000-0000-0000-000000000012"), "DEPENDIENTE_ENTIDADES_O_UNIVERSIDADES_PUBLICAS_CON_REGIMEN_ESPECIAL_EN_SALUD", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, 5, "Dependiente Entidades O Universidades Publicas Con Regimen Especial En Salud" },
                    { new Guid("20000000-0000-0000-0000-000000000013"), "PRE_PENSIONADO_DE_ENTIDAD_EN_LIQUIDACION", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, 5, "Pre Pensionado De Entidad En Liquidacion" },
                    { new Guid("20000000-0000-0000-0000-000000000014"), "ESTUDIANTES_APORTES_SOLO_RIESGOS_LABORALES", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, 5, "Estudiantes Aportes Solo Riesgos Laborales" },
                    { new Guid("20000000-0000-0000-0000-000000000015"), "TRABAJADOR_DE_TIEMPO_PARCIAL", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, 5, "Trabajador De Tiempo Parcial" },
                    { new Guid("20000000-0000-0000-0000-000000000016"), "APRENDICES_DEL_SENA_EN_ETAPA_PRODUCTIVA", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, 5, "Aprendices Del Sena En Etapa Productiva" },
                    { new Guid("20000000-0000-0000-0000-000000000017"), "APRENDICES_DEL_SENA_EN_ETAPA_PRODUCTIVA_REFORMA_2025", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, 5, "Aprendices Del Sena En Etapa Productiva Reforma 2025" },
                    { new Guid("20000000-0000-0000-0000-000000000018"), "APRENDICES_DEL_SENA_EN_ETAPA_LECTIVA_REFORMA_2025", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, 5, "Aprendices Del Sena En Etapa Lectiva Reforma 2025" },
                    { new Guid("30000000-0000-0000-0000-000000000001"), "TERMINO_FIJO", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, 6, "Termino Fijo" },
                    { new Guid("30000000-0000-0000-0000-000000000002"), "TERMINO_INDEFINIDO", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, 6, "Termino Indefinido" },
                    { new Guid("30000000-0000-0000-0000-000000000003"), "OBRA_LABOR", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, 6, "Obra Labor" },
                    { new Guid("30000000-0000-0000-0000-000000000004"), "APRENDIZAJE", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, 6, "Aprendizaje" },
                    { new Guid("30000000-0000-0000-0000-000000000005"), "PRACTICAS_PASANTIAS", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, 6, "Practicas Pasantias" },
                    { new Guid("40000000-0000-0000-0000-000000000001"), "CTX", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, 7, "Ctx" },
                    { new Guid("40000000-0000-0000-0000-000000000002"), "VALES", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, 7, "Vales" },
                    { new Guid("40000000-0000-0000-0000-000000000003"), "NOTA_PROMISORIA_FIRMADA_PRO_EL_BANCO", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, 7, "Nota Promisoria Firmada Pro El Banco" },
                    { new Guid("40000000-0000-0000-0000-000000000004"), "GIRO_URGENTE", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, 7, "Giro Urgente" },
                    { new Guid("40000000-0000-0000-0000-000000000005"), "CONCENTRACION_EFECTIVO_AHORROS_/_DESEMBOLSO_CREDITO_CCD", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, 7, "Concentración Efectivo/Ahorros - Desembolso Crédito CCD" },
                    { new Guid("40000000-0000-0000-0000-000000000006"), "REVERSION_CREDITO_AHORRO", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, 7, "Reversion Credito Ahorro" },
                    { new Guid("40000000-0000-0000-0000-000000000007"), "DEBITO_CTX", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, 7, "Debito Ctx" },
                    { new Guid("40000000-0000-0000-0000-000000000008"), "NOTA_RETIRO", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, 7, "Nota Retiro" },
                    { new Guid("40000000-0000-0000-0000-000000000009"), "NOTA_CAMBIARIA", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, 7, "Nota Cambiaria" },
                    { new Guid("40000000-0000-0000-0000-000000000010"), "NOTA_RETIRO_TERCERO", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, 7, "Nota Retiro Tercero" },
                    { new Guid("40000000-0000-0000-0000-000000000011"), "EFECTIVO", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, 7, "Efectivo" },
                    { new Guid("40000000-0000-0000-0000-000000000012"), "CHEQUE_LOCAL_TRAFERIBLE", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, 7, "Cheque Local Traferible" },
                    { new Guid("40000000-0000-0000-0000-000000000013"), "NOTA_BANCARIA_TRANFERIBLE", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, 7, "Nota Bancaria Tranferible" },
                    { new Guid("40000000-0000-0000-0000-000000000014"), "BOOKENTRY_DEBITO", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, 7, "Bookentry Debito" },
                    { new Guid("40000000-0000-0000-0000-000000000015"), "NOTA_PROMISORIA_FIRMADA_POR_EL_ACREEDOR_AVALADA_POR_UN_TERCERO", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, 7, "Nota Promisoria Firmada Por El Acreedor Avalada Por Un Tercero" },
                    { new Guid("40000000-0000-0000-0000-000000000016"), "PAGO_TESORERIA_URGENTE", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, 7, "Pago Tesoreria Urgente" },
                    { new Guid("40000000-0000-0000-0000-000000000017"), "REVERSION_CREDITO_DE_DEMANDA_ACH", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, 7, "Reversion Credito De Demanda Ach" },
                    { new Guid("40000000-0000-0000-0000-000000000018"), "ACUERDO_MUTUO", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, 7, "Acuerdo Mutuo" },
                    { new Guid("40000000-0000-0000-0000-000000000019"), "TARJETA_CREDITO", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, 7, "Tarjeta Credito" },
                    { new Guid("40000000-0000-0000-0000-000000000020"), "BONOS", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, 7, "Bonos" },
                    { new Guid("40000000-0000-0000-0000-000000000021"), "DESEMBOLSO_PLUS_DEBITO", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, 7, "Desembolso Plus Debito" },
                    { new Guid("40000000-0000-0000-0000-000000000022"), "CREDITO_AHORRO", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, 7, "Credito Ahorro" },
                    { new Guid("40000000-0000-0000-0000-000000000023"), "BOOKENTRY_CREDITO", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, 7, "Bookentry Credito" },
                    { new Guid("40000000-0000-0000-0000-000000000024"), "METODO_DE_PAGO_SOLICITADO_NO_USUADO", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, 7, "Metodo De Pago Solicitado No Usuado" },
                    { new Guid("40000000-0000-0000-0000-000000000025"), "TELEX_ESTANDAR_BANCARIO_FRANCES", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, 7, "Telex Estandar Bancario Frances" },
                    { new Guid("40000000-0000-0000-0000-000000000026"), "CREDITO_ACH", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, 7, "Credito Ach" },
                    { new Guid("40000000-0000-0000-0000-000000000027"), "CLEARING_ENTRE_PARTNERS", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, 7, "Clearing Entre Partners" },
                    { new Guid("40000000-0000-0000-0000-000000000028"), "DESEMBOLSO_DEBITO", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, 7, "Desembolso Debito" },
                    { new Guid("40000000-0000-0000-0000-000000000029"), "DEBITO_DE_DEMANDA_ACH", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, 7, "Debito De Demanda Ach" },
                    { new Guid("40000000-0000-0000-0000-000000000030"), "INSTRUMENTO_NO_DEFINIDO", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, 7, "Instrumento No Definido" },
                    { new Guid("40000000-0000-0000-0000-000000000031"), "TRANSFERENCIA_DEBITO_INTERBANCARIO", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, 7, "Transferencia Debito Interbancario" },
                    { new Guid("40000000-0000-0000-0000-000000000032"), "DESEMBOLSO_CREDITO_PLUS", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, 7, "Desembolso Credito Plus" },
                    { new Guid("40000000-0000-0000-0000-000000000033"), "PAGO_DEPOSITO_PRE_ACORDADO", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, 7, "Pago Deposito Pre Acordado" },
                    { new Guid("40000000-0000-0000-0000-000000000034"), "NOTA_PROMISORIA_FIRMADA_POR_UN_BANCO_AVALADA_POR_OTRO_BANCO", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, 7, "Nota Promisoria Firmada Por Un Banco Avalada Por Otro Banco" },
                    { new Guid("40000000-0000-0000-0000-000000000035"), "NOTA_PROMISORIA_FIRMADA", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, 7, "Nota Promisoria Firmada" },
                    { new Guid("40000000-0000-0000-0000-000000000036"), "CHEQUE_BANCARIO", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, 7, "Cheque Bancario" },
                    { new Guid("40000000-0000-0000-0000-000000000037"), "CHEQUE", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, 7, "Cheque" },
                    { new Guid("40000000-0000-0000-0000-000000000038"), "NOTA_PROMISORIA", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, 7, "Nota Promisoria" },
                    { new Guid("40000000-0000-0000-0000-000000000039"), "POSTGIRO", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, 7, "Postgiro" },
                    { new Guid("40000000-0000-0000-0000-000000000040"), "RETIRO_DE_NOTA_POR_EL_POR_EL_ACREEDOR_SOBRE_UN_BANCO", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, 7, "Retiro De Nota Por El Por El Acreedor Sobre Un Banco" },
                    { new Guid("40000000-0000-0000-0000-000000000041"), "PAGO_COMERCIAL_URGENTE", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, 7, "Pago Comercial Urgente" },
                    { new Guid("40000000-0000-0000-0000-000000000042"), "RETIRO_DE_NOTA_POR_EL_ACREEDOR_AVALADA_POR_OTRO_BANCO", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, 7, "Retiro De Nota Por El Acreedor Avalada Por Otro Banco" },
                    { new Guid("40000000-0000-0000-0000-000000000043"), "DEBITO_ACH", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, 7, "Debito Ach" },
                    { new Guid("40000000-0000-0000-0000-000000000044"), "TARJETA_DEBITO", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, 7, "Tarjeta Debito" },
                    { new Guid("40000000-0000-0000-0000-000000000045"), "NOTA_PROMISORIA_FIRMADA_ACREEDOR", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, 7, "Nota Promisoria Firmada Acreedor" },
                    { new Guid("40000000-0000-0000-0000-000000000046"), "PROYECTO_BANCARIO", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, 7, "Proyecto Bancario" },
                    { new Guid("40000000-0000-0000-0000-000000000047"), "NOTA_PROMISORIA_BANCO", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, 7, "Nota Promisoria Banco" },
                    { new Guid("40000000-0000-0000-0000-000000000048"), "PAGO_NEGOCIO_CORPORATIVO_AHORROS_CREDITO_CTP", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, 7, "Pago Negocio Corporativo Ahorros Credito Ctp" },
                    { new Guid("40000000-0000-0000-0000-000000000049"), "PAGO_NEGOCIO_CORPORATIVO_AHORROS_DEBITO_CTP", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, 7, "Pago Negocio Corporativo Ahorros Debito Ctp" },
                    { new Guid("40000000-0000-0000-0000-000000000050"), "CONSIGNACION_BANCARIA", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, 7, "Consignacion Bancaria" },
                    { new Guid("40000000-0000-0000-0000-000000000051"), "CHEQUE_LOCAL", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, 7, "Cheque Local" },
                    { new Guid("40000000-0000-0000-0000-000000000052"), "CREDITO_CTP", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, 7, "Credito Ctp" },
                    { new Guid("40000000-0000-0000-0000-000000000053"), "GIRO_REFERENCIADO", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, 7, "Giro Referenciado" },
                    { new Guid("50000000-0000-0000-0000-000000000001"), "SIMPLIFICADO", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, 8, "Régimen Simplificado" },
                    { new Guid("50000000-0000-0000-0000-000000000002"), "COMUN", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, 8, "Régimen Común" },
                    { new Guid("50000000-0000-0000-0000-000000000003"), "RESPONSABLE_DE_IVA", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, 8, "Responsable de IVA" },
                    { new Guid("50000000-0000-0000-0000-000000000004"), "NO_RESPONSABLE_DE_IVA", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, 8, "No Responsable de IVA" },
                    { new Guid("60000000-0000-0000-0000-000000000001"), "SIMPLE", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, 9, "Régimen Simple" },
                    { new Guid("60000000-0000-0000-0000-000000000002"), "ORDINARIO", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, 9, "Régimen Ordinario" },
                    { new Guid("60000000-0000-0000-0000-000000000003"), "AUTORRETENEDOR", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, 9, "Autorretenedor" },
                    { new Guid("70000000-0000-0000-0000-000000000001"), "AHORROS", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, 10, "Ahorros" },
                    { new Guid("70000000-0000-0000-0000-000000000002"), "CORRIENTE", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, 10, "Corriente" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000001"));

            migrationBuilder.DeleteData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000002"));

            migrationBuilder.DeleteData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000003"));

            migrationBuilder.DeleteData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000004"));

            migrationBuilder.DeleteData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000005"));

            migrationBuilder.DeleteData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000006"));

            migrationBuilder.DeleteData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000007"));

            migrationBuilder.DeleteData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000008"));

            migrationBuilder.DeleteData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000009"));

            migrationBuilder.DeleteData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000010"));

            migrationBuilder.DeleteData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000011"));

            migrationBuilder.DeleteData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000012"));

            migrationBuilder.DeleteData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000013"));

            migrationBuilder.DeleteData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000014"));

            migrationBuilder.DeleteData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000015"));

            migrationBuilder.DeleteData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000016"));

            migrationBuilder.DeleteData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000017"));

            migrationBuilder.DeleteData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000018"));

            migrationBuilder.DeleteData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000001"));

            migrationBuilder.DeleteData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000002"));

            migrationBuilder.DeleteData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000003"));

            migrationBuilder.DeleteData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000004"));

            migrationBuilder.DeleteData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000005"));

            migrationBuilder.DeleteData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000001"));

            migrationBuilder.DeleteData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000002"));

            migrationBuilder.DeleteData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000003"));

            migrationBuilder.DeleteData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000004"));

            migrationBuilder.DeleteData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000005"));

            migrationBuilder.DeleteData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000006"));

            migrationBuilder.DeleteData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000007"));

            migrationBuilder.DeleteData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000008"));

            migrationBuilder.DeleteData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000009"));

            migrationBuilder.DeleteData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000010"));

            migrationBuilder.DeleteData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000011"));

            migrationBuilder.DeleteData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000012"));

            migrationBuilder.DeleteData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000013"));

            migrationBuilder.DeleteData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000014"));

            migrationBuilder.DeleteData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000015"));

            migrationBuilder.DeleteData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000016"));

            migrationBuilder.DeleteData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000017"));

            migrationBuilder.DeleteData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000018"));

            migrationBuilder.DeleteData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000019"));

            migrationBuilder.DeleteData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000020"));

            migrationBuilder.DeleteData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000021"));

            migrationBuilder.DeleteData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000022"));

            migrationBuilder.DeleteData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000023"));

            migrationBuilder.DeleteData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000024"));

            migrationBuilder.DeleteData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000025"));

            migrationBuilder.DeleteData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000026"));

            migrationBuilder.DeleteData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000027"));

            migrationBuilder.DeleteData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000028"));

            migrationBuilder.DeleteData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000029"));

            migrationBuilder.DeleteData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000030"));

            migrationBuilder.DeleteData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000031"));

            migrationBuilder.DeleteData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000032"));

            migrationBuilder.DeleteData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000033"));

            migrationBuilder.DeleteData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000034"));

            migrationBuilder.DeleteData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000035"));

            migrationBuilder.DeleteData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000036"));

            migrationBuilder.DeleteData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000037"));

            migrationBuilder.DeleteData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000038"));

            migrationBuilder.DeleteData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000039"));

            migrationBuilder.DeleteData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000040"));

            migrationBuilder.DeleteData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000041"));

            migrationBuilder.DeleteData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000042"));

            migrationBuilder.DeleteData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000043"));

            migrationBuilder.DeleteData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000044"));

            migrationBuilder.DeleteData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000045"));

            migrationBuilder.DeleteData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000046"));

            migrationBuilder.DeleteData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000047"));

            migrationBuilder.DeleteData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000048"));

            migrationBuilder.DeleteData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000049"));

            migrationBuilder.DeleteData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000050"));

            migrationBuilder.DeleteData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000051"));

            migrationBuilder.DeleteData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000052"));

            migrationBuilder.DeleteData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000053"));

            migrationBuilder.DeleteData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("50000000-0000-0000-0000-000000000001"));

            migrationBuilder.DeleteData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("50000000-0000-0000-0000-000000000002"));

            migrationBuilder.DeleteData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("50000000-0000-0000-0000-000000000003"));

            migrationBuilder.DeleteData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("50000000-0000-0000-0000-000000000004"));

            migrationBuilder.DeleteData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("60000000-0000-0000-0000-000000000001"));

            migrationBuilder.DeleteData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("60000000-0000-0000-0000-000000000002"));

            migrationBuilder.DeleteData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("60000000-0000-0000-0000-000000000003"));

            migrationBuilder.DeleteData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("70000000-0000-0000-0000-000000000001"));

            migrationBuilder.DeleteData(
                table: "DataicoTaxCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("70000000-0000-0000-0000-000000000002"));

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "DataicoTaxCatalogItems",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "Category",
                table: "DataicoTaxCatalogItems",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(150)",
                oldMaxLength: 150);
        }
    }
}
