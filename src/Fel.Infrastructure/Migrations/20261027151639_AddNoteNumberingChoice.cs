using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddNoteNumberingChoice : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_NoteNumberings_ClientId_ResolutionId_BranchId_Kind",
                table: "NoteNumberings");

            migrationBuilder.AddColumn<bool>(
                name: "IsDefault",
                table: "NoteNumberings",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "NoteNumberingId",
                table: "Documents",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_NoteNumberings_ClientId_ResolutionId_BranchId_Kind_Prefix",
                table: "NoteNumberings",
                columns: new[] { "ClientId", "ResolutionId", "BranchId", "Kind", "Prefix" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_NoteNumberings_ClientId_ResolutionId_BranchId_Kind_Prefix",
                table: "NoteNumberings");

            migrationBuilder.DropColumn(
                name: "IsDefault",
                table: "NoteNumberings");

            migrationBuilder.DropColumn(
                name: "NoteNumberingId",
                table: "Documents");

            migrationBuilder.CreateIndex(
                name: "IX_NoteNumberings_ClientId_ResolutionId_BranchId_Kind",
                table: "NoteNumberings",
                columns: new[] { "ClientId", "ResolutionId", "BranchId", "Kind" },
                unique: true);
        }
    }
}
