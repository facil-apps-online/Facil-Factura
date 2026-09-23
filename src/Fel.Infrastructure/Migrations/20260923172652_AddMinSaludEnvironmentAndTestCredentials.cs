using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMinSaludEnvironmentAndTestCredentials : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // EF generó acá un RenameColumn de MinSaludApiBaseUrl a MinSaludUserType porque son
            // dos nullable string y adivinó que era un renombre. No lo es: la URL desapareció del
            // modelo (pasó a configuración de la plataforma, "MinSalud:MuvProductionUrl") y el
            // tipo de usuario es un campo nuevo del LoginSISPRO. Se separa en drop + add para que
            // ningún valor de URL termine dentro de un campo que espera RE, PIN, PINx o PIE.
            // La columna de URL está vacía en todos los Clients, así que no se pierde nada.
            migrationBuilder.DropColumn(
                name: "MinSaludApiBaseUrl",
                table: "Clients");

            migrationBuilder.AddColumn<string>(
                name: "MinSaludUserType",
                table: "Clients",
                type: "nvarchar(max)",
                nullable: true);

            // Por defecto "Test", no cadena vacía: un Client existente no debería quedar apuntando
            // a un ambiente inválido, y menos empezar a emitir contra producción sin que nadie lo
            // haya decidido.
            migrationBuilder.AddColumn<string>(
                name: "MinSaludEnvironment",
                table: "Clients",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "Test");

            migrationBuilder.AddColumn<string>(
                name: "MinSaludTestIdentificationNumber",
                table: "Clients",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MinSaludTestIdentificationType",
                table: "Clients",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MinSaludTestPasswordEncrypted",
                table: "Clients",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MinSaludEnvironment",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "MinSaludTestIdentificationNumber",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "MinSaludTestIdentificationType",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "MinSaludTestPasswordEncrypted",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "MinSaludUserType",
                table: "Clients");

            migrationBuilder.AddColumn<string>(
                name: "MinSaludApiBaseUrl",
                table: "Clients",
                type: "nvarchar(max)",
                nullable: true);
        }
    }
}
