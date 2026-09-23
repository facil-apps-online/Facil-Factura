using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddClientIntegratorAssignments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ClientIntegratorAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IntegratorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EffectiveFrom = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EffectiveTo = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClientIntegratorAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClientIntegratorAssignments_Clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClientIntegratorAssignments_Integrators_IntegratorId",
                        column: x => x.IntegratorId,
                        principalTable: "Integrators",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ClientIntegratorAssignments_ClientId",
                table: "ClientIntegratorAssignments",
                column: "ClientId",
                unique: true,
                filter: "[EffectiveTo] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ClientIntegratorAssignments_IntegratorId",
                table: "ClientIntegratorAssignments",
                column: "IntegratorId");

            // Backfill: cada Client existente arranca su historial en su propio CreatedAt con el
            // Integrator que tiene asignado hoy — es la mejor aproximación posible, ya que antes de
            // esta tabla no se guardaba cuándo cambiaba de integrador.
            migrationBuilder.Sql(
                "INSERT INTO [ClientIntegratorAssignments] ([Id], [ClientId], [IntegratorId], [EffectiveFrom], [EffectiveTo]) " +
                "SELECT NEWID(), [Id], [IntegratorId], [CreatedAt], NULL FROM [Clients];");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClientIntegratorAssignments");
        }
    }
}
