using Fel.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fel.Infrastructure.Migrations
{
    // Sin estos atributos EF no la descubre: una base nueva no recibiría la columna ResolutionId (en producción ya está aplicada).
    [DbContext(typeof(FelDbContext))]
    [Migration("20261023190000_LinkNoteNumberingToResolution")]
    public partial class LinkNoteNumberingToResolution : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ResolutionId",
                table: "NoteNumberings",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.Sql(@"
UPDATE n
SET ResolutionId = r.Id
FROM NoteNumberings n
CROSS APPLY (
    SELECT TOP 1 Id FROM Resolutions r
    WHERE r.ClientId = n.ClientId AND r.DocumentType = 'FE' AND r.IsActive = 1
    ORDER BY r.IsDefault DESC, r.ValidTo DESC
) r
WHERE n.ResolutionId IS NULL AND n.Kind IN (1, 2);");

            migrationBuilder.DropIndex(name: "IX_NoteNumberings_ClientId_BranchId_Kind", table: "NoteNumberings");
            migrationBuilder.DropIndex(name: "IX_NoteNumberings_ClientId_Kind_Prefix", table: "NoteNumberings");

            migrationBuilder.CreateIndex(
                name: "IX_NoteNumberings_ClientId_ResolutionId_BranchId_Kind",
                table: "NoteNumberings",
                columns: new[] { "ClientId", "ResolutionId", "BranchId", "Kind" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NoteNumberings_ResolutionId",
                table: "NoteNumberings",
                column: "ResolutionId");

            migrationBuilder.AddForeignKey(
                name: "FK_NoteNumberings_Resolutions_ResolutionId",
                table: "NoteNumberings",
                column: "ResolutionId",
                principalTable: "Resolutions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(name: "FK_NoteNumberings_Resolutions_ResolutionId", table: "NoteNumberings");
            migrationBuilder.DropIndex(name: "IX_NoteNumberings_ResolutionId", table: "NoteNumberings");
            migrationBuilder.DropIndex(name: "IX_NoteNumberings_ClientId_ResolutionId_BranchId_Kind", table: "NoteNumberings");
            migrationBuilder.CreateIndex(name: "IX_NoteNumberings_ClientId_BranchId_Kind", table: "NoteNumberings", columns: new[] { "ClientId", "BranchId", "Kind" }, unique: true);
            migrationBuilder.CreateIndex(name: "IX_NoteNumberings_ClientId_Kind_Prefix", table: "NoteNumberings", columns: new[] { "ClientId", "Kind", "Prefix" }, unique: true, filter: "[Prefix] IS NOT NULL");
            migrationBuilder.DropColumn(name: "ResolutionId", table: "NoteNumberings");
        }
    }
}
