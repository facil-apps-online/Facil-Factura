using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUserSessionSecurity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SecurityStamp",
                table: "TenantUsers",
                type: "uniqueidentifier",
                nullable: false,
                defaultValueSql: "NEWID()");

            migrationBuilder.AddColumn<int>(
                name: "SessionMinutes",
                table: "TenantUsers",
                type: "int",
                nullable: false,
                defaultValue: 30);

            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "SuperadminUsers",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "SecurityStamp",
                table: "SuperadminUsers",
                type: "uniqueidentifier",
                nullable: false,
                defaultValueSql: "NEWID()");

            migrationBuilder.AddColumn<int>(
                name: "SessionMinutes",
                table: "SuperadminUsers",
                type: "int",
                nullable: false,
                defaultValue: 30);

            migrationBuilder.AddColumn<Guid>(
                name: "SecurityStamp",
                table: "DeveloperUsers",
                type: "uniqueidentifier",
                nullable: false,
                defaultValueSql: "NEWID()");

            migrationBuilder.AddColumn<int>(
                name: "SessionMinutes",
                table: "DeveloperUsers",
                type: "int",
                nullable: false,
                defaultValue: 30);

            migrationBuilder.AddColumn<Guid>(
                name: "SecurityStamp",
                table: "ClientUsers",
                type: "uniqueidentifier",
                nullable: false,
                defaultValueSql: "NEWID()");

            migrationBuilder.AddColumn<int>(
                name: "SessionMinutes",
                table: "ClientUsers",
                type: "int",
                nullable: false,
                defaultValue: 30);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SecurityStamp",
                table: "TenantUsers");

            migrationBuilder.DropColumn(
                name: "SessionMinutes",
                table: "TenantUsers");

            migrationBuilder.DropColumn(
                name: "Name",
                table: "SuperadminUsers");

            migrationBuilder.DropColumn(
                name: "SecurityStamp",
                table: "SuperadminUsers");

            migrationBuilder.DropColumn(
                name: "SessionMinutes",
                table: "SuperadminUsers");

            migrationBuilder.DropColumn(
                name: "SecurityStamp",
                table: "DeveloperUsers");

            migrationBuilder.DropColumn(
                name: "SessionMinutes",
                table: "DeveloperUsers");

            migrationBuilder.DropColumn(
                name: "SecurityStamp",
                table: "ClientUsers");

            migrationBuilder.DropColumn(
                name: "SessionMinutes",
                table: "ClientUsers");
        }
    }
}
