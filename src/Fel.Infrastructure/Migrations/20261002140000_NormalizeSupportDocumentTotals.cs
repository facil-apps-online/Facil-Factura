using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fel.Infrastructure.Migrations
{
    // El Documento Soporte guardaba TotalAmount ya descontado el descuento general; ahora, igual que
    // las facturas, guarda el bruto (subtotal + IVA) y el neto se calcula al mostrar/imprimir. Los
    // documentos existentes con descuento se llevan al mismo criterio para que no se resten dos veces.
    public partial class NormalizeSupportDocumentTotals : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"UPDATE Documents SET TotalAmount = TotalAmount + GeneralDiscountAmount
WHERE TypeCode IN ('DS', 'DS-AJUSTE') AND GeneralDiscountAmount IS NOT NULL AND GeneralDiscountAmount > 0");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"UPDATE Documents SET TotalAmount = TotalAmount - GeneralDiscountAmount
WHERE TypeCode IN ('DS', 'DS-AJUSTE') AND GeneralDiscountAmount IS NOT NULL AND GeneralDiscountAmount > 0");
        }
    }
}
