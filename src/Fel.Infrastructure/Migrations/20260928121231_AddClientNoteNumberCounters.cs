using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddClientNoteNumberCounters : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "NextCreditNoteNumber",
                table: "Clients",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "NextDebitNoteNumber",
                table: "Clients",
                type: "bigint",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NextCreditNoteNumber",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "NextDebitNoteNumber",
                table: "Clients");
        }
    }
}
