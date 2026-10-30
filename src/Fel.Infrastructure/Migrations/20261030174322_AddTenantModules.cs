using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantModules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Los tenants existentes quedan con los dos módulos, tal como funcionaban: facturan y solicitan certificados para sus clientes.
            migrationBuilder.AddColumn<bool>(
                name: "CertificatesModuleEnabled",
                table: "Tenants",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "InvoicingModuleEnabled",
                table: "Tenants",
                type: "bit",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CertificatesModuleEnabled",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "InvoicingModuleEnabled",
                table: "Tenants");
        }
    }
}
