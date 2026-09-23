using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Fel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddIntegratorCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Integrators",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Nit = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Integrators", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "Integrators",
                columns: new[] { "Id", "Code", "IsActive", "Kind", "Name", "Nit" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0000-000000000101"), "NATIVE", true, 0, "Emisión directa DIAN", "" },
                    { new Guid("00000000-0000-0000-0000-000000000102"), "DATAICO", true, 1, "Dataico S.A.S.", "900.XXX.XXX-X" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Integrators_Code",
                table: "Integrators",
                column: "Code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Integrators");
        }
    }
}
