using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSandboxKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LiveApiKey",
                table: "Tenants",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "LiveApiSecret",
                table: "Tenants",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TestApiKey",
                table: "Tenants",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TestApiSecret",
                table: "Tenants",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_LiveApiKey",
                table: "Tenants",
                column: "LiveApiKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_TestApiKey",
                table: "Tenants",
                column: "TestApiKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Tenants_LiveApiKey",
                table: "Tenants");

            migrationBuilder.DropIndex(
                name: "IX_Tenants_TestApiKey",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "LiveApiKey",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "LiveApiSecret",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "TestApiKey",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "TestApiSecret",
                table: "Tenants");
        }
    }
}
