using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RefactorAuthAndApiKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
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

            migrationBuilder.AddColumn<string>(
                name: "LiveApiKey",
                table: "Clients",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "LiveApiSecret",
                table: "Clients",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TestApiKey",
                table: "Clients",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TestApiSecret",
                table: "Clients",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "ClientUsers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClientUsers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClientUsers_Clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TenantUsers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantUsers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TenantUsers_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Clients_LiveApiKey",
                table: "Clients",
                column: "LiveApiKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Clients_TestApiKey",
                table: "Clients",
                column: "TestApiKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClientUsers_ClientId",
                table: "ClientUsers",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_TenantUsers_TenantId",
                table: "TenantUsers",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClientUsers");

            migrationBuilder.DropTable(
                name: "TenantUsers");

            migrationBuilder.DropIndex(
                name: "IX_Clients_LiveApiKey",
                table: "Clients");

            migrationBuilder.DropIndex(
                name: "IX_Clients_TestApiKey",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "LiveApiKey",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "LiveApiSecret",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "TestApiKey",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "TestApiSecret",
                table: "Clients");

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
    }
}
