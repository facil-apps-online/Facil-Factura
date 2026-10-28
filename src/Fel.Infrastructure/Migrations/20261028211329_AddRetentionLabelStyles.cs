using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Fel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRetentionLabelStyles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FooterRetentionLabelStyle",
                table: "Clients",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "Short");

            migrationBuilder.AddColumn<string>(
                name: "LineRetentionLabelStyle",
                table: "Clients",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "Short");

            migrationBuilder.CreateTable(
                name: "RetentionShortNames",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TaxCategory = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ShortName = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RetentionShortNames", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "RetentionShortNames",
                columns: new[] { "Id", "ShortName", "TaxCategory" },
                values: new object[,]
                {
                    { new Guid("5c1f0001-0000-4000-8000-000000000001"), "RETE FUENTE", "RET_FUENTE" },
                    { new Guid("5c1f0001-0000-4000-8000-000000000002"), "RETE ICA", "RET_ICA" },
                    { new Guid("5c1f0001-0000-4000-8000-000000000003"), "RETE IVA", "RET_IVA" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_RetentionShortNames_TaxCategory",
                table: "RetentionShortNames",
                column: "TaxCategory",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RetentionShortNames");

            migrationBuilder.DropColumn(
                name: "FooterRetentionLabelStyle",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "LineRetentionLabelStyle",
                table: "Clients");
        }
    }
}
