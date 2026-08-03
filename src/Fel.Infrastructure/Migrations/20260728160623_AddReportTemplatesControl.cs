using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddReportTemplatesControl : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "UsedTemplateId",
                table: "Documents",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DocumentTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    RepxTemplateKey = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    VersionNumber = table.Column<int>(type: "int", nullable: false),
                    PreviousVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ClonedFromId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DocumentTypeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ClientId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentTemplates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentTemplates_Clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DocumentTemplates_DocumentTemplates_ClonedFromId",
                        column: x => x.ClonedFromId,
                        principalTable: "DocumentTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DocumentTemplates_DocumentTemplates_PreviousVersionId",
                        column: x => x.PreviousVersionId,
                        principalTable: "DocumentTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DocumentTemplates_DocumentTypes_DocumentTypeId",
                        column: x => x.DocumentTypeId,
                        principalTable: "DocumentTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DocumentTemplates_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ClientDocumentSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentTypeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SelectedTemplateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClientDocumentSettings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClientDocumentSettings_Clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ClientDocumentSettings_DocumentTemplates_SelectedTemplateId",
                        column: x => x.SelectedTemplateId,
                        principalTable: "DocumentTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClientDocumentSettings_DocumentTypes_DocumentTypeId",
                        column: x => x.DocumentTypeId,
                        principalTable: "DocumentTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Documents_UsedTemplateId",
                table: "Documents",
                column: "UsedTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientDocumentSettings_ClientId",
                table: "ClientDocumentSettings",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientDocumentSettings_DocumentTypeId",
                table: "ClientDocumentSettings",
                column: "DocumentTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientDocumentSettings_SelectedTemplateId",
                table: "ClientDocumentSettings",
                column: "SelectedTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentTemplates_ClientId",
                table: "DocumentTemplates",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentTemplates_ClonedFromId",
                table: "DocumentTemplates",
                column: "ClonedFromId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentTemplates_DocumentTypeId",
                table: "DocumentTemplates",
                column: "DocumentTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentTemplates_PreviousVersionId",
                table: "DocumentTemplates",
                column: "PreviousVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentTemplates_TenantId",
                table: "DocumentTemplates",
                column: "TenantId");

            migrationBuilder.AddForeignKey(
                name: "FK_Documents_DocumentTemplates_UsedTemplateId",
                table: "Documents",
                column: "UsedTemplateId",
                principalTable: "DocumentTemplates",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Documents_DocumentTemplates_UsedTemplateId",
                table: "Documents");

            migrationBuilder.DropTable(
                name: "ClientDocumentSettings");

            migrationBuilder.DropTable(
                name: "DocumentTemplates");

            migrationBuilder.DropIndex(
                name: "IX_Documents_UsedTemplateId",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "UsedTemplateId",
                table: "Documents");
        }
    }
}
