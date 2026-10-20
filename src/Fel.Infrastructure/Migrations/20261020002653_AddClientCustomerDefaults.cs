using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddClientCustomerDefaults : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CustomerAddress",
                table: "ClientInvoiceDefaults",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CustomerCityCode",
                table: "ClientInvoiceDefaults",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CustomerCityName",
                table: "ClientInvoiceDefaults",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CustomerPhone",
                table: "ClientInvoiceDefaults",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CustomerPostalCode",
                table: "ClientInvoiceDefaults",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CustomerAddress",
                table: "ClientInvoiceDefaults");

            migrationBuilder.DropColumn(
                name: "CustomerCityCode",
                table: "ClientInvoiceDefaults");

            migrationBuilder.DropColumn(
                name: "CustomerCityName",
                table: "ClientInvoiceDefaults");

            migrationBuilder.DropColumn(
                name: "CustomerPhone",
                table: "ClientInvoiceDefaults");

            migrationBuilder.DropColumn(
                name: "CustomerPostalCode",
                table: "ClientInvoiceDefaults");
        }
    }
}
