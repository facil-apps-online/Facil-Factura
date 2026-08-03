using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRipsValidationRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RipsCie10Rules",
                columns: table => new
                {
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    AllowedGender = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    MinAgeYears = table.Column<int>(type: "int", nullable: false),
                    MaxAgeYears = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RipsCie10Rules", x => x.Code);
                });

            migrationBuilder.CreateTable(
                name: "RipsCupsRules",
                columns: table => new
                {
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    AllowedGender = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    MinAgeDays = table.Column<int>(type: "int", nullable: false),
                    MaxAgeDays = table.Column<int>(type: "int", nullable: false),
                    RequiresDiagnosis = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RipsCupsRules", x => x.Code);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RipsCie10Rules");

            migrationBuilder.DropTable(
                name: "RipsCupsRules");
        }
    }
}
