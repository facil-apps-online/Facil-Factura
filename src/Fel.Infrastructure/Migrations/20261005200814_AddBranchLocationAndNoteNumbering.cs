using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBranchLocationAndNoteNumbering : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Address",
                table: "Branches",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "City",
                table: "Branches",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CityCode",
                table: "Branches",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Email",
                table: "Branches",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Phone",
                table: "Branches",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "NoteNumberings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    Prefix = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    NextNumber = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NoteNumberings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NoteNumberings_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_NoteNumberings_Clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NoteNumberings_BranchId",
                table: "NoteNumberings",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_NoteNumberings_ClientId_BranchId_Kind",
                table: "NoteNumberings",
                columns: new[] { "ClientId", "BranchId", "Kind" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NoteNumberings_ClientId_Kind_Prefix",
                table: "NoteNumberings",
                columns: new[] { "ClientId", "Kind", "Prefix" },
                unique: true,
                filter: "[Prefix] IS NOT NULL");

            // Los contadores de notas pasan de Client a NoteNumberings: una fila compartida (sin sucursal) por tipo y cliente, con los
            // valores actuales. Idempotente: no duplica si se repite. Las columnas de Client se conservan hasta la limpieza final.
            migrationBuilder.Sql(@"
                INSERT INTO NoteNumberings (Id, ClientId, BranchId, Kind, Prefix, NextNumber)
                SELECT NEWID(), c.Id, NULL, 1, NULL, c.NextCreditNoteNumber
                FROM Clients c
                WHERE NOT EXISTS (SELECT 1 FROM NoteNumberings n WHERE n.ClientId = c.Id AND n.BranchId IS NULL AND n.Kind = 1);");

            migrationBuilder.Sql(@"
                INSERT INTO NoteNumberings (Id, ClientId, BranchId, Kind, Prefix, NextNumber)
                SELECT NEWID(), c.Id, NULL, 2, NULL, c.NextDebitNoteNumber
                FROM Clients c
                WHERE NOT EXISTS (SELECT 1 FROM NoteNumberings n WHERE n.ClientId = c.Id AND n.BranchId IS NULL AND n.Kind = 2);");

            migrationBuilder.Sql(@"
                INSERT INTO NoteNumberings (Id, ClientId, BranchId, Kind, Prefix, NextNumber)
                SELECT NEWID(), c.Id, NULL, 3, NULLIF(LTRIM(RTRIM(c.SupportAdjustmentPrefix)), ''), c.NextSupportAdjustmentNumber
                FROM Clients c
                WHERE NOT EXISTS (SELECT 1 FROM NoteNumberings n WHERE n.ClientId = c.Id AND n.BranchId IS NULL AND n.Kind = 3);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NoteNumberings");

            migrationBuilder.DropColumn(
                name: "Address",
                table: "Branches");

            migrationBuilder.DropColumn(
                name: "City",
                table: "Branches");

            migrationBuilder.DropColumn(
                name: "CityCode",
                table: "Branches");

            migrationBuilder.DropColumn(
                name: "Email",
                table: "Branches");

            migrationBuilder.DropColumn(
                name: "Phone",
                table: "Branches");
        }
    }
}
