using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddExportInvoiceFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DeliveryTermsCode",
                table: "Documents",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeliveryTermsName",
                table: "Documents",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BrandName",
                table: "DocumentItems",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModelName",
                table: "DocumentItems",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TariffCode",
                table: "DocumentItems",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            // Condiciones de entrega de la factura de exportación (tabla 13.3.7 del anexo técnico de la DIAN, Incoterms).
            // Repetible: no inserta un código que ya exista.
            migrationBuilder.Sql(@"
INSERT INTO TaxCatalogItems (Id, Category, Name, Kind, IsActive, CreatedAt)
SELECT NEWID(), v.Code, v.Name, 16, 1, GETUTCDATE()
FROM (VALUES
(N'CFR', N'Costo y flete'),
(N'CIF', N'Costo, flete y seguro'),
(N'CIP', N'Transporte y Seguro Pagados hasta'),
(N'CPT', N'Transporte Pagado Hasta'),
(N'DAP', N'Entregado en un Lugar'),
(N'DDP', N'Entregado con Pago de Derechos'),
(N'DPU', N'Entregada en Lugar Descargada'),
(N'EXW', N'En Fábrica'),
(N'FAS', N'Franco al costado del buque'),
(N'FCA', N'Franco transportista'),
(N'FOB', N'Franco a bordo')
) AS v(Code, Name)
WHERE NOT EXISTS (SELECT 1 FROM TaxCatalogItems t WHERE t.Kind = 16 AND t.Category = v.Code);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM TaxCatalogItems WHERE Kind = 16;");

            migrationBuilder.DropColumn(
                name: "DeliveryTermsCode",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "DeliveryTermsName",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "BrandName",
                table: "DocumentItems");

            migrationBuilder.DropColumn(
                name: "ModelName",
                table: "DocumentItems");

            migrationBuilder.DropColumn(
                name: "TariffCode",
                table: "DocumentItems");
        }
    }
}
