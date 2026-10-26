using Fel.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fel.Infrastructure.Migrations
{
    // Completa el catálogo de medios de pago (Kind = 4) con los valores que acepta Dataico y su código de la tabla de medios de pago de la DIAN
    // (MediosPago-2.1.gc de la caja de herramientas). Por Category y sin pisar lo que ya se configuró desde el portal: lo que falta se
    // inserta, y a lo existente solo se le completa el código DIAN cuando está vacío.
    [DbContext(typeof(FelDbContext))]
    [Migration("20261026200000_CompletePaymentMeansCatalog")]
    public partial class CompletePaymentMeansCatalog : Migration
    {
        private static readonly (string Category, string Name, string DianCode)[] Means =
        {
            ("CHEQUE", "Cheque", "20"),
            ("CREDIT_ACH", "Crédito ACH", "2"),
            ("DEBIT_ACH", "Débito ACH", "3"),
            ("CREDIT_TRANSFER", "Transferencia Crédito", "30"),
            ("DEBIT_TRANSFER", "Transferencia Débito", "31"),
            ("CREDIT_BANK_TRANSFER", "Transferencia Crédito Bancario", "45"),
            ("DEBIT_INTERBANK_TRANSFER", "Transferencia Débito Interbancario", "46"),
            ("DEBIT_BANK_TRANSFER", "Transferencia Débito Bancaria", "47"),
            ("GIRO_REFERENCIADO", "Giro Referenciado", "93"),
            ("INSTRUMENTO_NO_DEFINIDO", "Instrumento no definido", "1"),
            ("OTHER", "Otro", "1"),
            ("MUTUAL_AGREEMENT", "Acuerdo Mutuo", "ZZZ"),
            ("CREDIT_AHORRO", "Crédito Ahorro", "13"),
            ("DEBIT_AHORRO", "Débito Ahorro", "14"),
            ("CHEQUE_LOCAL_TRANSFERIBLE", "Cheque Local Transferible", "92"),
            ("GIRO_URGENTE", "Giro Urgente", "94"),
            ("PAGO_COMERCIAL_URGENTE", "Pago Comercial Urgente", "52"),
        };

        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var (category, name, dianCode) in Means)
            {
                migrationBuilder.Sql($@"
IF EXISTS (SELECT 1 FROM dbo.TaxCatalogItems WHERE Kind = 4 AND Category = N'{category}')
    UPDATE dbo.TaxCatalogItems SET DianCode = N'{dianCode}' WHERE Kind = 4 AND Category = N'{category}' AND (DianCode IS NULL OR DianCode = N'');
ELSE
    INSERT INTO dbo.TaxCatalogItems (Id, Category, Name, Kind, Rate, DianCode, WordsName, IsActive, CreatedAt)
    VALUES (NEWID(), N'{category}', N'{name}', 4, NULL, N'{dianCode}', NULL, 1, SYSUTCDATETIME());");
            }

            // "Transferencia Crédito" quedó como nombre de CREDIT_BANK_TRANSFER, que en la DIAN es "Transferencia Crédito Bancario" (45);
            // el 30 es CREDIT_TRANSFER. Solo se corrige si el nombre sigue siendo el de la carga manual.
            migrationBuilder.Sql(@"
UPDATE dbo.TaxCatalogItems SET Name = N'Transferencia Crédito Bancario'
WHERE Kind = 4 AND Category = N'CREDIT_BANK_TRANSFER' AND Name = N'Transferencia Crédito';");

            // Borradores y rechazados que quedaron con los valores viejos del catálogo, que Dataico ya no acepta. Los aprobados no se tocan.
            migrationBuilder.Sql(@"
UPDATE dbo.Documents SET PaymentMeans = N'BANK_TRANSFER' WHERE PaymentMeans = N'TRANSFERENCIA' AND Status IN (N'DRAFT', N'REJECTED');
UPDATE dbo.Documents SET PaymentMeans = N'CASH' WHERE PaymentMeans = N'EFECTIVO' AND Status IN (N'DRAFT', N'REJECTED');
UPDATE dbo.ClientInvoiceDefaults SET PaymentMeans = N'BANK_TRANSFER' WHERE PaymentMeans = N'TRANSFERENCIA';
UPDATE dbo.ClientInvoiceDefaults SET PaymentMeans = N'CASH' WHERE PaymentMeans = N'EFECTIVO';");
        }

        // Datos de catálogo: no se deshace.
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
