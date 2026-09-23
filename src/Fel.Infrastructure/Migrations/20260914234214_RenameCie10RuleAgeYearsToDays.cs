using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RenameCie10RuleAgeYearsToDays : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "MinAgeYears",
                table: "RipsCie10Rules",
                newName: "MinAgeDays");

            migrationBuilder.RenameColumn(
                name: "MaxAgeYears",
                table: "RipsCie10Rules",
                newName: "MaxAgeDays");

            // Los valores existentes se cargaron mal (años en vez de días, ver RipsDataSeeder.cs).
            // Se limpia la tabla para que RipsDataSeeder la vuelva a poblar con el decodificador
            // correcto (unidad+valor) en el próximo arranque.
            migrationBuilder.Sql("DELETE FROM RipsCie10Rules;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "MinAgeDays",
                table: "RipsCie10Rules",
                newName: "MinAgeYears");

            migrationBuilder.RenameColumn(
                name: "MaxAgeDays",
                table: "RipsCie10Rules",
                newName: "MaxAgeYears");
        }
    }
}
