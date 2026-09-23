using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddClientEnabledCatalogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ClientEnabledDocumentTypes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentTypeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClientEnabledDocumentTypes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClientEnabledDocumentTypes_Clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ClientEnabledDocumentTypes_DocumentTypes_DocumentTypeId",
                        column: x => x.DocumentTypeId,
                        principalTable: "DocumentTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ClientEnabledRetentionConcepts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RetentionConceptId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClientEnabledRetentionConcepts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClientEnabledRetentionConcepts_Clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ClientEnabledRetentionConcepts_RetentionConcepts_RetentionConceptId",
                        column: x => x.RetentionConceptId,
                        principalTable: "RetentionConcepts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ClientEnabledDocumentTypes_ClientId_DocumentTypeId",
                table: "ClientEnabledDocumentTypes",
                columns: new[] { "ClientId", "DocumentTypeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClientEnabledDocumentTypes_DocumentTypeId",
                table: "ClientEnabledDocumentTypes",
                column: "DocumentTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientEnabledRetentionConcepts_ClientId_RetentionConceptId",
                table: "ClientEnabledRetentionConcepts",
                columns: new[] { "ClientId", "RetentionConceptId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClientEnabledRetentionConcepts_RetentionConceptId",
                table: "ClientEnabledRetentionConcepts",
                column: "RetentionConceptId");

            // Backfill: los Clients que ya existían antes de este mecanismo de habilitación
            // arrancan con el mismo set estándar que se siembra ahora al crear uno nuevo (ver
            // Fel.Core.Entities.DefaultCatalogSets) — sin esto, perderían de golpe la capacidad de
            // emitir Factura/NC/ND y de elegir retenciones, porque los endpoints de client-web ya
            // filtran por estas tablas en vez de usar la lista global de antes.
            var documentTypeIds = new[]
            {
                "00000000-0000-0000-0000-000000000001", // FE-STD
                "00000000-0000-0000-0000-000000000009", // NC
                "00000000-0000-0000-0000-000000000010"  // ND
            };
            foreach (var documentTypeId in documentTypeIds)
            {
                migrationBuilder.Sql(
                    $"INSERT INTO [ClientEnabledDocumentTypes] ([Id], [ClientId], [DocumentTypeId]) " +
                    $"SELECT NEWID(), [Id], '{documentTypeId}' FROM [Clients];");
            }

            var retentionConceptIds = new[]
            {
                "30000000-0000-0000-0000-000000000001", // Compras generales (declarantes)
                "30000000-0000-0000-0000-000000000002", // Compras generales (no declarantes)
                "3000000f-0000-0000-0000-000000000015", // Servicios generales (declarantes)
                "30000010-0000-0000-0000-000000000016", // Servicios generales (no declarantes)
                "30000021-0000-0000-0000-000000000033", // Honorarios y comisiones (personas jurídicas)
                "30000022-0000-0000-0000-000000000034"  // Honorarios y comisiones (personas naturales, no declarantes)
            };
            foreach (var retentionConceptId in retentionConceptIds)
            {
                migrationBuilder.Sql(
                    $"INSERT INTO [ClientEnabledRetentionConcepts] ([Id], [ClientId], [RetentionConceptId]) " +
                    $"SELECT NEWID(), [Id], '{retentionConceptId}' FROM [Clients];");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClientEnabledDocumentTypes");

            migrationBuilder.DropTable(
                name: "ClientEnabledRetentionConcepts");
        }
    }
}
