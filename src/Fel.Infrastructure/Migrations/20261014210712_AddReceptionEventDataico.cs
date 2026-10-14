using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddReceptionEventDataico : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "EventNumber",
                table: "ReceivedDocumentEvents",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProviderUid",
                table: "ReceivedDocumentEvents",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReceptionEventPrefix",
                table: "Clients",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "EV");

            migrationBuilder.AddColumn<int>(
                name: "ReceptionEventStartNumber",
                table: "Clients",
                type: "int",
                nullable: false,
                defaultValue: 1);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EventNumber",
                table: "ReceivedDocumentEvents");

            migrationBuilder.DropColumn(
                name: "ProviderUid",
                table: "ReceivedDocumentEvents");

            migrationBuilder.DropColumn(
                name: "ReceptionEventPrefix",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "ReceptionEventStartNumber",
                table: "Clients");
        }
    }
}
