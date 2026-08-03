using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCreditNoteReference : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ReferenceConcept",
                table: "Documents",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "ReferenceDocumentId",
                table: "Documents",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Documents_ReferenceDocumentId",
                table: "Documents",
                column: "ReferenceDocumentId");

            migrationBuilder.AddForeignKey(
                name: "FK_Documents_Documents_ReferenceDocumentId",
                table: "Documents",
                column: "ReferenceDocumentId",
                principalTable: "Documents",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Documents_Documents_ReferenceDocumentId",
                table: "Documents");

            migrationBuilder.DropIndex(
                name: "IX_Documents_ReferenceDocumentId",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "ReferenceConcept",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "ReferenceDocumentId",
                table: "Documents");
        }
    }
}
