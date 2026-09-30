using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSmtpSettingsToClientAndTenant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SmtpFromEmail",
                table: "Tenants",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SmtpFromName",
                table: "Tenants",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SmtpHost",
                table: "Tenants",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SmtpPasswordEncrypted",
                table: "Tenants",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SmtpPort",
                table: "Tenants",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "SmtpUseSsl",
                table: "Tenants",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "SmtpUser",
                table: "Tenants",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SmtpFromEmail",
                table: "Clients",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SmtpFromName",
                table: "Clients",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SmtpHost",
                table: "Clients",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SmtpPasswordEncrypted",
                table: "Clients",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SmtpPort",
                table: "Clients",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "SmtpUseSsl",
                table: "Clients",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "SmtpUser",
                table: "Clients",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SmtpFromEmail",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "SmtpFromName",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "SmtpHost",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "SmtpPasswordEncrypted",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "SmtpPort",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "SmtpUseSsl",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "SmtpUser",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "SmtpFromEmail",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "SmtpFromName",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "SmtpHost",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "SmtpPasswordEncrypted",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "SmtpPort",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "SmtpUseSsl",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "SmtpUser",
                table: "Clients");
        }
    }
}
