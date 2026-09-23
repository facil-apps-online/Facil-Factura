using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddResolutionSelectorToDocument : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ResolutionId",
                table: "Documents",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Documents_ResolutionId",
                table: "Documents",
                column: "ResolutionId");

            migrationBuilder.AddForeignKey(
                name: "FK_Documents_Resolutions_ResolutionId",
                table: "Documents",
                column: "ResolutionId",
                principalTable: "Resolutions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Documents_Resolutions_ResolutionId",
                table: "Documents");

            migrationBuilder.DropIndex(
                name: "IX_Documents_ResolutionId",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "ResolutionId",
                table: "Documents");
        }
    }
}
