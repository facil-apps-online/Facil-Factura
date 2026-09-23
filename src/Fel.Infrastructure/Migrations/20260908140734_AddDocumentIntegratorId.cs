using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentIntegratorId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "IntegratorId",
                table: "Documents",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Documents_IntegratorId",
                table: "Documents",
                column: "IntegratorId");

            migrationBuilder.AddForeignKey(
                name: "FK_Documents_Integrators_IntegratorId",
                table: "Documents",
                column: "IntegratorId",
                principalTable: "Integrators",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Documents_Integrators_IntegratorId",
                table: "Documents");

            migrationBuilder.DropIndex(
                name: "IX_Documents_IntegratorId",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "IntegratorId",
                table: "Documents");
        }
    }
}
