using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddClientCustomerFiscalDefaults : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CustomerCountryCode",
                table: "ClientInvoiceDefaults",
                type: "nvarchar(2)",
                maxLength: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CustomerRegimen",
                table: "ClientInvoiceDefaults",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CustomerTaxLevelCode",
                table: "ClientInvoiceDefaults",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CustomerCountryCode",
                table: "ClientInvoiceDefaults");

            migrationBuilder.DropColumn(
                name: "CustomerRegimen",
                table: "ClientInvoiceDefaults");

            migrationBuilder.DropColumn(
                name: "CustomerTaxLevelCode",
                table: "ClientInvoiceDefaults");
        }
    }
}
