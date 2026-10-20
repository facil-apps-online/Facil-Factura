using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantBlocks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ParentBlockReason",
                table: "Tenants",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ParentBlockedAt",
                table: "Tenants",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ParentBlockedBy",
                table: "Tenants",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PlatformBlockReason",
                table: "Tenants",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PlatformBlockedAt",
                table: "Tenants",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PlatformBlockedBy",
                table: "Tenants",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ParentBlockReason",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "ParentBlockedAt",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "ParentBlockedBy",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "PlatformBlockReason",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "PlatformBlockedAt",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "PlatformBlockedBy",
                table: "Tenants");
        }
    }
}
