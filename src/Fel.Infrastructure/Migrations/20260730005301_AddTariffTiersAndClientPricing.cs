using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Fel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTariffTiersAndClientPricing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "PricePerDocument",
                table: "Clients",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "TariffTiers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MinDocuments = table.Column<int>(type: "int", nullable: false),
                    MaxDocuments = table.Column<int>(type: "int", nullable: true),
                    PricePerDocument = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TariffTiers", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "TariffTiers",
                columns: new[] { "Id", "IsActive", "MaxDocuments", "MinDocuments", "Name", "PricePerDocument" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0000-000000000001"), true, 2000, 1, "Nivel 1", 70m },
                    { new Guid("00000000-0000-0000-0000-000000000002"), true, 5000, 2001, "Nivel 2", 50m },
                    { new Guid("00000000-0000-0000-0000-000000000003"), true, 10000, 5001, "Nivel 3", 40m },
                    { new Guid("00000000-0000-0000-0000-000000000004"), true, 100000, 10001, "Nivel 4", 30m },
                    { new Guid("00000000-0000-0000-0000-000000000005"), true, null, 100001, "Nivel 5", 20m }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TariffTiers");

            migrationBuilder.DropColumn(
                name: "PricePerDocument",
                table: "Clients");
        }
    }
}
