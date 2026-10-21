using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddClientUserInvitedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "InvitedAt",
                table: "ClientUsers",
                type: "datetime2",
                nullable: true);

            // Los usuarios que ya existen siguen siendo "invitados": solo los que se creen sin invitación (InvitedAt nulo) figuran como pendientes.
            migrationBuilder.Sql("UPDATE ClientUsers SET InvitedAt = CreatedAt WHERE InvitedAt IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "InvitedAt",
                table: "ClientUsers");
        }
    }
}
