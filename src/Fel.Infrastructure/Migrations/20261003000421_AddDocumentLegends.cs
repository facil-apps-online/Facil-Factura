using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fel.Infrastructure.Migrations
{
    public partial class AddDocumentLegends : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DocumentLegendByPrefixes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Prefix = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Text = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentLegendByPrefixes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentLegendByPrefixes_Clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentLegendByPrefixes_ClientId_DocumentType_Prefix",
                table: "DocumentLegendByPrefixes",
                columns: new[] { "ClientId", "DocumentType", "Prefix" },
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "DocumentLegendByPrefixes");
        }
    }
}
