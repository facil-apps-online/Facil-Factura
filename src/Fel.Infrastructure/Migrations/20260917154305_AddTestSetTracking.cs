using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTestSetTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TestSetLastInvoiceCufe",
                table: "Clients",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TestSetLastInvoiceNumber",
                table: "Clients",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TestSetRequiredAcceptedCreditNotes",
                table: "Clients",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TestSetRequiredAcceptedDebitNotes",
                table: "Clients",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TestSetRequiredAcceptedInvoices",
                table: "Clients",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TestSetRequiredCreditNotes",
                table: "Clients",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TestSetRequiredDebitNotes",
                table: "Clients",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TestSetRequiredInvoices",
                table: "Clients",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TestSetSentCreditNotes",
                table: "Clients",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TestSetSentDebitNotes",
                table: "Clients",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TestSetSentInvoices",
                table: "Clients",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TestSetLastInvoiceCufe",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "TestSetLastInvoiceNumber",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "TestSetRequiredAcceptedCreditNotes",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "TestSetRequiredAcceptedDebitNotes",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "TestSetRequiredAcceptedInvoices",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "TestSetRequiredCreditNotes",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "TestSetRequiredDebitNotes",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "TestSetRequiredInvoices",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "TestSetSentCreditNotes",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "TestSetSentDebitNotes",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "TestSetSentInvoices",
                table: "Clients");
        }
    }
}
