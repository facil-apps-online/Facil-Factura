using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Fel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SeedRetentionEngineCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            var effectiveFrom = new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);

            migrationBuilder.InsertData(
                table: "TaxParameters",
                columns: new[] { "Id", "Code", "Value", "EffectiveFrom", "CreatedAt" },
                values: new object[,]
                {
                    { new Guid("40000000-0000-0000-0000-000000000001"), "UVT", 52374m, effectiveFrom, effectiveFrom }
                });

            // Tarifas de Retención en la Fuente 2026 (DIAN, UVT 2026 = $52.374). PersonType: 0=Ambas,
            // 1=Natural, 2=Juridica. BaseType: 0=Subtotal, 1=IvaGenerado. Deja fuera del alcance,
            // deliberadamente, la sección "Retención por pagos al exterior" del PDF de referencia.
            migrationBuilder.InsertData(
                table: "RetentionConcepts",
                columns: new[] { "Id", "GroupKey", "GroupLabel", "Name", "PersonType", "TaxCategory", "BaseType", "BaseUvt", "Rate", "EffectiveFrom", "IsActive", "CreatedAt" },
                values: new object[,]
                {
                    { new Guid("30000000-0000-0000-0000-000000000001"), "COMPRAS_GENERALES", "Compras generales", "Compras generales (declarantes)", 2, "RET_FUENTE", 0, 10m, 2.5m, effectiveFrom, true, effectiveFrom },
                    { new Guid("30000000-0000-0000-0000-000000000002"), "COMPRAS_GENERALES", "Compras generales", "Compras generales (no declarantes)", 1, "RET_FUENTE", 0, 10m, 3.5m, effectiveFrom, true, effectiveFrom },
                    { new Guid("30000000-0000-0000-0000-000000000003"), "COMPRAS_TARJETA", "Compras con tarjeta débito o crédito", "Compras con tarjeta débito o crédito", 0, "RET_FUENTE", 0, 0m, 1.5m, effectiveFrom, true, effectiveFrom },
                    { new Guid("30000000-0000-0000-0000-000000000004"), "COMPRAS_AGRICOLAS_SIN_PROCESAR", "Compras de bienes o productos agrícolas o pecuarios sin procesamiento industrial", "Compras de bienes o productos agrícolas o pecuarios sin procesamiento industrial", 0, "RET_FUENTE", 0, 70m, 1.5m, effectiveFrom, true, effectiveFrom },
                    { new Guid("30000000-0000-0000-0000-000000000005"), "COMPRAS_AGRICOLAS_PROCESADAS", "Compras de bienes o productos agrícolas o pecuarios con procesamiento industrial", "Compras de bienes o productos agrícolas o pecuarios con procesamiento industrial (declarantes)", 2, "RET_FUENTE", 0, 10m, 2.5m, effectiveFrom, true, effectiveFrom },
                    { new Guid("30000000-0000-0000-0000-000000000006"), "COMPRAS_AGRICOLAS_PROCESADAS", "Compras de bienes o productos agrícolas o pecuarios con procesamiento industrial", "Compras de bienes o productos agrícolas o pecuarios con procesamiento industrial (no declarantes)", 1, "RET_FUENTE", 0, 10m, 3.5m, effectiveFrom, true, effectiveFrom },
                    { new Guid("30000000-0000-0000-0000-000000000007"), "COMPRAS_CAFE", "Compras de café pergamino o cereza", "Compras de café pergamino o cereza", 0, "RET_FUENTE", 0, 70m, 0.5m, effectiveFrom, true, effectiveFrom },
                    { new Guid("30000000-0000-0000-0000-000000000008"), "COMPRAS_COMBUSTIBLES", "Compras de combustibles derivados del petróleo", "Compras de combustibles derivados del petróleo", 0, "RET_FUENTE", 0, 0m, 0.1m, effectiveFrom, true, effectiveFrom },
                    { new Guid("30000000-0000-0000-0000-000000000009"), "ENAJENACION_ACTIVOS_FIJOS_PN", "Enajenación de activos fijos de personas naturales", "Enajenación de activos fijos de personas naturales", 0, "RET_FUENTE", 0, 0m, 1m, effectiveFrom, true, effectiveFrom },
                    { new Guid("3000000a-0000-0000-0000-000000000010"), "COMPRAS_VEHICULOS", "Compras de vehículos", "Compras de vehículos", 0, "RET_FUENTE", 0, 0m, 1m, effectiveFrom, true, effectiveFrom },
                    { new Guid("3000000b-0000-0000-0000-000000000011"), "COMPRA_ORO_SCI", "Compra de oro por sociedades de comercialización internacional", "Compra de oro por sociedades de comercialización internacional", 0, "RET_FUENTE", 0, 0m, 2.5m, effectiveFrom, true, effectiveFrom },
                    { new Guid("3000000c-0000-0000-0000-000000000012"), "COMPRAS_INMUEBLES_VIVIENDA_BASE", "Compras de bienes raíces destinación vivienda (primeras 10.000 UVT)", "Compras de bienes raíces destinación vivienda (primeras 10.000 UVT)", 0, "RET_FUENTE", 0, 0m, 1m, effectiveFrom, true, effectiveFrom },
                    { new Guid("3000000d-0000-0000-0000-000000000013"), "COMPRAS_INMUEBLES_VIVIENDA_EXCESO", "Compras de bienes raíces destinación vivienda (exceso de 10.000 UVT)", "Compras de bienes raíces destinación vivienda (exceso de 10.000 UVT)", 0, "RET_FUENTE", 0, 20000m, 2.5m, effectiveFrom, true, effectiveFrom },
                    { new Guid("3000000e-0000-0000-0000-000000000014"), "COMPRAS_INMUEBLES_NO_VIVIENDA", "Compras de bienes raíces destinación distinta a vivienda", "Compras de bienes raíces destinación distinta a vivienda", 0, "RET_FUENTE", 0, 0m, 2.5m, effectiveFrom, true, effectiveFrom },
                    { new Guid("3000000f-0000-0000-0000-000000000015"), "SERVICIOS_GENERALES", "Servicios generales", "Servicios generales (declarantes)", 2, "RET_FUENTE", 0, 2m, 4m, effectiveFrom, true, effectiveFrom },
                    { new Guid("30000010-0000-0000-0000-000000000016"), "SERVICIOS_GENERALES", "Servicios generales", "Servicios generales (no declarantes)", 1, "RET_FUENTE", 0, 2m, 6m, effectiveFrom, true, effectiveFrom },
                    { new Guid("30000011-0000-0000-0000-000000000017"), "EMOLUMENTOS_ECLESIASTICOS", "Emolumentos eclesiásticos", "Emolumentos eclesiásticos (declarantes)", 2, "RET_FUENTE", 0, 10m, 4m, effectiveFrom, true, effectiveFrom },
                    { new Guid("30000012-0000-0000-0000-000000000018"), "EMOLUMENTOS_ECLESIASTICOS", "Emolumentos eclesiásticos", "Emolumentos eclesiásticos (no declarantes)", 1, "RET_FUENTE", 0, 10m, 3.5m, effectiveFrom, true, effectiveFrom },
                    { new Guid("30000013-0000-0000-0000-000000000019"), "TRANSPORTE_CARGA", "Servicios de transporte de carga", "Servicios de transporte de carga", 0, "RET_FUENTE", 0, 2m, 1m, effectiveFrom, true, effectiveFrom },
                    { new Guid("30000014-0000-0000-0000-000000000020"), "TRANSPORTE_PASAJEROS_TERRESTRE", "Transporte nacional de pasajeros por vía terrestre", "Transporte nacional de pasajeros por vía terrestre (declarantes)", 2, "RET_FUENTE", 0, 10m, 3.5m, effectiveFrom, true, effectiveFrom },
                    { new Guid("30000015-0000-0000-0000-000000000021"), "TRANSPORTE_PASAJEROS_TERRESTRE", "Transporte nacional de pasajeros por vía terrestre", "Transporte nacional de pasajeros por vía terrestre (no declarantes)", 1, "RET_FUENTE", 0, 10m, 3.5m, effectiveFrom, true, effectiveFrom },
                    { new Guid("30000016-0000-0000-0000-000000000022"), "TRANSPORTE_PASAJEROS_AEREO_MARITIMO", "Transporte nacional de pasajeros por vía aérea o marítima", "Transporte nacional de pasajeros por vía aérea o marítima", 0, "RET_FUENTE", 0, 2m, 1m, effectiveFrom, true, effectiveFrom },
                    { new Guid("30000017-0000-0000-0000-000000000023"), "SERVICIOS_TEMPORALES_AIU", "Servicios de empresas de servicios temporales (sobre AIU)", "Servicios de empresas de servicios temporales (sobre AIU)", 0, "RET_FUENTE", 0, 2m, 1m, effectiveFrom, true, effectiveFrom },
                    { new Guid("30000018-0000-0000-0000-000000000024"), "VIGILANCIA_ASEO_AIU", "Servicios de vigilancia y aseo (sobre AIU)", "Servicios de vigilancia y aseo (sobre AIU)", 0, "RET_FUENTE", 0, 2m, 2m, effectiveFrom, true, effectiveFrom },
                    { new Guid("30000019-0000-0000-0000-000000000025"), "SALUD_IPS", "Servicios integrales de salud prestados por IPS", "Servicios integrales de salud prestados por IPS", 0, "RET_FUENTE", 0, 2m, 2m, effectiveFrom, true, effectiveFrom },
                    { new Guid("3000001a-0000-0000-0000-000000000026"), "HOTELES_RESTAURANTES", "Servicios de hoteles y restaurantes", "Servicios de hoteles y restaurantes (declarantes)", 2, "RET_FUENTE", 0, 2m, 3.5m, effectiveFrom, true, effectiveFrom },
                    { new Guid("3000001b-0000-0000-0000-000000000027"), "HOTELES_RESTAURANTES", "Servicios de hoteles y restaurantes", "Servicios de hoteles y restaurantes (no declarantes)", 1, "RET_FUENTE", 0, 2m, 3.5m, effectiveFrom, true, effectiveFrom },
                    { new Guid("3000001c-0000-0000-0000-000000000028"), "ARRENDAMIENTO_MUEBLES", "Arrendamiento de bienes muebles", "Arrendamiento de bienes muebles", 0, "RET_FUENTE", 0, 0m, 4m, effectiveFrom, true, effectiveFrom },
                    { new Guid("3000001d-0000-0000-0000-000000000029"), "ARRENDAMIENTO_INMUEBLES", "Arrendamiento de bienes inmuebles", "Arrendamiento de bienes inmuebles (declarantes)", 2, "RET_FUENTE", 0, 10m, 3.5m, effectiveFrom, true, effectiveFrom },
                    { new Guid("3000001e-0000-0000-0000-000000000030"), "ARRENDAMIENTO_INMUEBLES", "Arrendamiento de bienes inmuebles", "Arrendamiento de bienes inmuebles (no declarantes)", 1, "RET_FUENTE", 0, 10m, 3.5m, effectiveFrom, true, effectiveFrom },
                    { new Guid("3000001f-0000-0000-0000-000000000031"), "OTROS_INGRESOS", "Otros ingresos tributarios", "Otros ingresos tributarios (declarantes)", 2, "RET_FUENTE", 0, 10m, 2.5m, effectiveFrom, true, effectiveFrom },
                    { new Guid("30000020-0000-0000-0000-000000000032"), "OTROS_INGRESOS", "Otros ingresos tributarios", "Otros ingresos tributarios (no declarantes)", 1, "RET_FUENTE", 0, 10m, 3.5m, effectiveFrom, true, effectiveFrom },
                    { new Guid("30000021-0000-0000-0000-000000000033"), "HONORARIOS_COMISIONES", "Honorarios y comisiones", "Honorarios y comisiones (personas jurídicas)", 2, "RET_FUENTE", 0, 0m, 11m, effectiveFrom, true, effectiveFrom },
                    { new Guid("30000022-0000-0000-0000-000000000034"), "HONORARIOS_COMISIONES", "Honorarios y comisiones", "Honorarios y comisiones (personas naturales, no declarantes)", 1, "RET_FUENTE", 0, 0m, 10m, effectiveFrom, true, effectiveFrom },
                    { new Guid("30000023-0000-0000-0000-000000000035"), "LICENCIAMIENTO_SOFTWARE", "Servicios de licenciamiento o derecho de uso de software", "Servicios de licenciamiento o derecho de uso de software", 0, "RET_FUENTE", 0, 0m, 3.5m, effectiveFrom, true, effectiveFrom },
                    { new Guid("30000024-0000-0000-0000-000000000036"), "INTERESES_RENDIMIENTOS", "Intereses o rendimientos financieros", "Intereses o rendimientos financieros", 0, "RET_FUENTE", 0, 0m, 7m, effectiveFrom, true, effectiveFrom },
                    { new Guid("30000025-0000-0000-0000-000000000037"), "RENDIMIENTOS_RENTA_FIJA", "Rendimientos financieros de títulos de renta fija", "Rendimientos financieros de títulos de renta fija", 0, "RET_FUENTE", 0, 0m, 4m, effectiveFrom, true, effectiveFrom },
                    { new Guid("30000026-0000-0000-0000-000000000038"), "LOTERIAS_RIFAS", "Loterías, rifas, apuestas y similares", "Loterías, rifas, apuestas y similares", 0, "RET_FUENTE", 0, 48m, 20m, effectiveFrom, true, effectiveFrom },
                    { new Guid("30000027-0000-0000-0000-000000000039"), "JUEGOS_SUERTE_AZAR", "Colocación independiente de juegos de suerte y azar", "Colocación independiente de juegos de suerte y azar", 0, "RET_FUENTE", 0, 5m, 3m, effectiveFrom, true, effectiveFrom },
                    { new Guid("30000028-0000-0000-0000-000000000040"), "CONSTRUCCION_URBANIZACION", "Contratos de construcción y urbanización", "Contratos de construcción y urbanización", 0, "RET_FUENTE", 0, 10m, 2m, effectiveFrom, true, effectiveFrom },
                    { new Guid("30000029-0000-0000-0000-000000000041"), "RETEIVA_SERVICIOS", "Retención en la fuente por IVA en servicios", "Retención en la fuente por IVA en servicios", 0, "RET_IVA", 1, 2m, 15m, effectiveFrom, true, effectiveFrom },
                    { new Guid("3000002a-0000-0000-0000-000000000042"), "RETEIVA_COMPRAS", "Retención en la fuente por IVA en compras", "Retención en la fuente por IVA en compras", 0, "RET_IVA", 1, 10m, 15m, effectiveFrom, true, effectiveFrom }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(table: "TaxParameters", keyColumn: "Id", keyValue: new Guid("40000000-0000-0000-0000-000000000001"));

            var ids = new[]
            {
                "30000000-0000-0000-0000-000000000001", "30000000-0000-0000-0000-000000000002", "30000000-0000-0000-0000-000000000003",
                "30000000-0000-0000-0000-000000000004", "30000000-0000-0000-0000-000000000005", "30000000-0000-0000-0000-000000000006",
                "30000000-0000-0000-0000-000000000007", "30000000-0000-0000-0000-000000000008", "30000000-0000-0000-0000-000000000009",
                "3000000a-0000-0000-0000-000000000010", "3000000b-0000-0000-0000-000000000011", "3000000c-0000-0000-0000-000000000012",
                "3000000d-0000-0000-0000-000000000013", "3000000e-0000-0000-0000-000000000014", "3000000f-0000-0000-0000-000000000015",
                "30000010-0000-0000-0000-000000000016", "30000011-0000-0000-0000-000000000017", "30000012-0000-0000-0000-000000000018",
                "30000013-0000-0000-0000-000000000019", "30000014-0000-0000-0000-000000000020", "30000015-0000-0000-0000-000000000021",
                "30000016-0000-0000-0000-000000000022", "30000017-0000-0000-0000-000000000023", "30000018-0000-0000-0000-000000000024",
                "30000019-0000-0000-0000-000000000025", "3000001a-0000-0000-0000-000000000026", "3000001b-0000-0000-0000-000000000027",
                "3000001c-0000-0000-0000-000000000028", "3000001d-0000-0000-0000-000000000029", "3000001e-0000-0000-0000-000000000030",
                "3000001f-0000-0000-0000-000000000031", "30000020-0000-0000-0000-000000000032", "30000021-0000-0000-0000-000000000033",
                "30000022-0000-0000-0000-000000000034", "30000023-0000-0000-0000-000000000035", "30000024-0000-0000-0000-000000000036",
                "30000025-0000-0000-0000-000000000037", "30000026-0000-0000-0000-000000000038", "30000027-0000-0000-0000-000000000039",
                "30000028-0000-0000-0000-000000000040", "30000029-0000-0000-0000-000000000041", "3000002a-0000-0000-0000-000000000042"
            };
            foreach (var id in ids)
            {
                migrationBuilder.DeleteData(table: "RetentionConcepts", keyColumn: "Id", keyValue: new Guid(id));
            }
        }
    }
}
