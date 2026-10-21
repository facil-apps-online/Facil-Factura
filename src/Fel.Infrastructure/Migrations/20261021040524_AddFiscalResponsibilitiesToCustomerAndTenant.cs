using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFiscalResponsibilitiesToCustomerAndTenant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "IncomeTaxRegime",
                table: "Tenants",
                type: "nvarchar(2)",
                maxLength: 2,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsAgenteRetenedorIva",
                table: "Tenants",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsAutorretenedorRenta",
                table: "Tenants",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsGranContribuyente",
                table: "Tenants",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "IncomeTaxRegime",
                table: "Customers",
                type: "nvarchar(2)",
                maxLength: 2,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsAgenteRetenedorIva",
                table: "Customers",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsAutorretenedorRenta",
                table: "Customers",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsGranContribuyente",
                table: "Customers",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IncomeTaxRegime",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "IsAgenteRetenedorIva",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "IsAutorretenedorRenta",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "IsGranContribuyente",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "IncomeTaxRegime",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "IsAgenteRetenedorIva",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "IsAutorretenedorRenta",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "IsGranContribuyente",
                table: "Customers");
        }
    }
}
