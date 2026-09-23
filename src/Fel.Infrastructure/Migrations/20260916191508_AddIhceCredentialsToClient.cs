using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddIhceCredentialsToClient : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "IhceApimSubscriptionKey",
                table: "Clients",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IhceClientId",
                table: "Clients",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IhceClientSecretEncrypted",
                table: "Clients",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IhceEndpoint",
                table: "Clients",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IhceEnvironment",
                table: "Clients",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "IhceTenantId",
                table: "Clients",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IhceApimSubscriptionKey",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "IhceClientId",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "IhceClientSecretEncrypted",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "IhceEndpoint",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "IhceEnvironment",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "IhceTenantId",
                table: "Clients");
        }
    }
}
