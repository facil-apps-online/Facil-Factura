using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fel.Infrastructure.Migrations
{
    public partial class AddDocumentTypeReportTitle : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.DocumentTypes', 'ReportTitle') IS NULL
BEGIN
    ALTER TABLE dbo.DocumentTypes ADD ReportTitle nvarchar(255) NOT NULL
        CONSTRAINT DF_DocumentTypes_ReportTitle DEFAULT(N'');
END;
 ");
            migrationBuilder.Sql(@"
UPDATE dbo.DocumentTypes SET ReportTitle = CASE Code
    WHEN 'FE-STD' THEN N'FACTURA ELECTRÓNICA DE VENTA'
    WHEN 'FE-SALUD' THEN N'FACTURA ELECTRÓNICA DE VENTA'
    WHEN 'FE-AIU' THEN N'FACTURA ELECTRÓNICA DE VENTA'
    WHEN 'FE-MANDATO' THEN N'FACTURA ELECTRÓNICA DE VENTA'
    WHEN 'FE-TRANSP' THEN N'FACTURA ELECTRÓNICA DE VENTA'
    WHEN 'FE-EXP' THEN N'FACTURA ELECTRÓNICA DE EXPORTACIÓN'
    WHEN 'FC-FACT' THEN N'FACTURA DE CONTINGENCIA'
    WHEN 'FC-DIAN' THEN N'FACTURA DE CONTINGENCIA DIAN'
    WHEN 'NC' THEN N'NOTA CRÉDITO ELECTRÓNICA'
    WHEN 'ND' THEN N'NOTA DÉBITO ELECTRÓNICA'
    WHEN 'DS' THEN N'DOCUMENTO SOPORTE EN ADQUISICIONES EFECTUADAS A NO OBLIGADOS A FACTURAR'
    WHEN 'DS-AJUSTE' THEN N'NOTA DE AJUSTE - DOCUMENTO SOPORTE'
    WHEN 'NE-PAGO' THEN N'NÓMINA ELECTRÓNICA'
    WHEN 'NE-AJUSTE' THEN N'NOTA DE AJUSTE - NÓMINA ELECTRÓNICA'
    ELSE Name END WHERE ReportTitle = N'';");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.DocumentTypes', 'ReportTitle') IS NOT NULL
    ALTER TABLE dbo.DocumentTypes DROP CONSTRAINT IF EXISTS DF_DocumentTypes_ReportTitle;
IF COL_LENGTH('dbo.DocumentTypes', 'ReportTitle') IS NOT NULL
    ALTER TABLE dbo.DocumentTypes DROP COLUMN ReportTitle;");
        }
    }
}
