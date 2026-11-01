using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEmailColors : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EmailColorDark",
                table: "Tenants",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmailColorLight",
                table: "Tenants",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmailColorDark",
                table: "PlatformEmailSettings",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmailColorLight",
                table: "PlatformEmailSettings",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmailColorDark",
                table: "Clients",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmailColorLight",
                table: "Clients",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EmailColorDark",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "EmailColorLight",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "EmailColorDark",
                table: "PlatformEmailSettings");

            migrationBuilder.DropColumn(
                name: "EmailColorLight",
                table: "PlatformEmailSettings");

            migrationBuilder.DropColumn(
                name: "EmailColorDark",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "EmailColorLight",
                table: "Clients");
        }
    }
}
