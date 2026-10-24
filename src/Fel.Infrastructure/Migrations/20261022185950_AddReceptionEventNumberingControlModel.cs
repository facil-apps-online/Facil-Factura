using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddReceptionEventNumberingControlModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ReceptionEventNumberings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Prefix = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    NextNumber = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReceptionEventNumberings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReceptionEventNumberings_Clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReceptionEventNumberings_ClientId",
                table: "ReceptionEventNumberings",
                column: "ClientId",
                unique: true);

            migrationBuilder.Sql(@"
                INSERT INTO ReceptionEventNumberings (Id, ClientId, Prefix, NextNumber)
                SELECT NEWID(), c.Id,
                       COALESCE(NULLIF(LTRIM(RTRIM(r.Prefix)), ''), NULLIF(LTRIM(RTRIM(c.ReceptionEventPrefix)), ''), 'EV'),
                       CASE WHEN COALESCE(r.NextNumber, 0) > COALESCE(c.ReceptionEventStartNumber, 1)
                            THEN r.NextNumber ELSE COALESCE(c.ReceptionEventStartNumber, 1) END
                FROM Clients c
                OUTER APPLY (SELECT TOP 1 Prefix, NextNumber FROM Resolutions WHERE ClientId = c.Id AND DocumentType = 'RADIAN' ORDER BY IsActive DESC, IsDefault DESC, ValidFrom DESC) r
                WHERE NOT EXISTS (SELECT 1 FROM ReceptionEventNumberings n WHERE n.ClientId = c.Id);");

            migrationBuilder.Sql(@"
                UPDATE r SET IsActive = 0
                FROM Resolutions r
                WHERE r.DocumentType = 'RADIAN';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReceptionEventNumberings");
        }
    }
}
