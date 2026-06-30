using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ChangeDocumentTypeIdToGuid : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(name: "FK_TenantPricings_DocumentTypes_DocumentTypeId", table: "TenantPricings");
            migrationBuilder.DropForeignKey(name: "FK_Documents_DocumentTypes_DocumentTypeId", table: "Documents");

            migrationBuilder.DropTable(name: "DocumentTypes");

            migrationBuilder.CreateTable(
                name: "DocumentTypes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false, defaultValue: ""),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentTypes", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentTypes_Code",
                table: "DocumentTypes",
                column: "Code",
                unique: true);

            // Recrear columnas
            migrationBuilder.DropIndex(name: "IX_TenantPricings_DocumentTypeId", table: "TenantPricings");
            migrationBuilder.DropColumn(name: "DocumentTypeId", table: "TenantPricings");
            migrationBuilder.AddColumn<Guid>(name: "DocumentTypeId", table: "TenantPricings", type: "uniqueidentifier", nullable: false, defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.DropIndex(name: "IX_Documents_DocumentTypeId", table: "Documents");
            migrationBuilder.DropColumn(name: "DocumentTypeId", table: "Documents");
            migrationBuilder.AddColumn<Guid>(name: "DocumentTypeId", table: "Documents", type: "uniqueidentifier", nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TenantPricings_DocumentTypeId",
                table: "TenantPricings",
                column: "DocumentTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_DocumentTypeId",
                table: "Documents",
                column: "DocumentTypeId");

            migrationBuilder.AddForeignKey(
                name: "FK_TenantPricings_DocumentTypes_DocumentTypeId",
                table: "TenantPricings",
                column: "DocumentTypeId",
                principalTable: "DocumentTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Documents_DocumentTypes_DocumentTypeId",
                table: "Documents",
                column: "DocumentTypeId",
                principalTable: "DocumentTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Not needed for now
        }
    }
}
