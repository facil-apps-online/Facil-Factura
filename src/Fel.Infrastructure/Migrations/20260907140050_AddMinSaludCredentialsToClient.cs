using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMinSaludCredentialsToClient : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MinSaludApiBaseUrl",
                table: "Clients",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MinSaludIdentificationNumber",
                table: "Clients",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MinSaludIdentificationType",
                table: "Clients",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MinSaludPasswordEncrypted",
                table: "Clients",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MinSaludApiBaseUrl",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "MinSaludIdentificationNumber",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "MinSaludIdentificationType",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "MinSaludPasswordEncrypted",
                table: "Clients");
        }
    }
}
