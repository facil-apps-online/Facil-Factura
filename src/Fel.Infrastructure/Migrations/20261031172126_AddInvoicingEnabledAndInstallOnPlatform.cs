using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddInvoicingEnabledAndInstallOnPlatform : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "InvoicingEnabled",
                table: "Clients",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "InstallOnPlatform",
                table: "CertificateRequests",
                type: "bit",
                nullable: false,
                defaultValue: true);

            // Los clientes de un tenant sin facturación ya eran titulares: no deben pasar a facturar si luego se activa ese módulo.
            migrationBuilder.Sql("UPDATE Clients SET InvoicingEnabled = 0 WHERE TenantId IN (SELECT Id FROM Tenants WHERE InvoicingModuleEnabled = 0)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "InvoicingEnabled",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "InstallOnPlatform",
                table: "CertificateRequests");
        }
    }
}
