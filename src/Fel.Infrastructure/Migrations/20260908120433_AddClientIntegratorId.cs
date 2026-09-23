using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddClientIntegratorId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Se agrega IntegratorId todavía nullable, con todos los Clients existentes apuntando
            // a NATIVE por defecto, y luego se corrige a DATAICO los que tenían
            // DocumentProvider = 1 (Dataico) — así no se pierde el dato antes de poder mapearlo, a
            // diferencia de lo que habría hecho un DropColumn(DocumentProvider) primero.
            migrationBuilder.AddColumn<Guid>(
                name: "IntegratorId",
                table: "Clients",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000101"));

            migrationBuilder.Sql(
                "UPDATE [Clients] SET [IntegratorId] = '00000000-0000-0000-0000-000000000102' WHERE [DocumentProvider] = 1;");

            migrationBuilder.DropColumn(
                name: "DocumentProvider",
                table: "Clients");

            migrationBuilder.CreateIndex(
                name: "IX_Clients_IntegratorId",
                table: "Clients",
                column: "IntegratorId");

            migrationBuilder.AddForeignKey(
                name: "FK_Clients_Integrators_IntegratorId",
                table: "Clients",
                column: "IntegratorId",
                principalTable: "Integrators",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Clients_Integrators_IntegratorId",
                table: "Clients");

            migrationBuilder.DropIndex(
                name: "IX_Clients_IntegratorId",
                table: "Clients");

            migrationBuilder.AddColumn<int>(
                name: "DocumentProvider",
                table: "Clients",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql(
                "UPDATE [Clients] SET [DocumentProvider] = 1 WHERE [IntegratorId] = '00000000-0000-0000-0000-000000000102';");

            migrationBuilder.DropColumn(
                name: "IntegratorId",
                table: "Clients");
        }
    }
}
