using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddIntegratorBillingOverrides : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "IntegratorId",
                table: "TariffTiers",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ClientIntegratorBillings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IntegratorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Mode = table.Column<int>(type: "int", nullable: false),
                    PricePerDocument = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PricePerUser = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClientIntegratorBillings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClientIntegratorBillings_Clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClientIntegratorBillings_Integrators_IntegratorId",
                        column: x => x.IntegratorId,
                        principalTable: "Integrators",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TenantIntegratorBillings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IntegratorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Mode = table.Column<int>(type: "int", nullable: false),
                    PricePerUser = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantIntegratorBillings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TenantIntegratorBillings_Integrators_IntegratorId",
                        column: x => x.IntegratorId,
                        principalTable: "Integrators",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TenantIntegratorBillings_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.UpdateData(
                table: "TariffTiers",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "IntegratorId",
                value: null);

            migrationBuilder.UpdateData(
                table: "TariffTiers",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000002"),
                column: "IntegratorId",
                value: null);

            migrationBuilder.UpdateData(
                table: "TariffTiers",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000003"),
                column: "IntegratorId",
                value: null);

            migrationBuilder.UpdateData(
                table: "TariffTiers",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000004"),
                column: "IntegratorId",
                value: null);

            migrationBuilder.UpdateData(
                table: "TariffTiers",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000005"),
                column: "IntegratorId",
                value: null);

            migrationBuilder.CreateIndex(
                name: "IX_TariffTiers_IntegratorId",
                table: "TariffTiers",
                column: "IntegratorId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientIntegratorBillings_ClientId_IntegratorId",
                table: "ClientIntegratorBillings",
                columns: new[] { "ClientId", "IntegratorId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClientIntegratorBillings_IntegratorId",
                table: "ClientIntegratorBillings",
                column: "IntegratorId");

            migrationBuilder.CreateIndex(
                name: "IX_TenantIntegratorBillings_IntegratorId",
                table: "TenantIntegratorBillings",
                column: "IntegratorId");

            migrationBuilder.CreateIndex(
                name: "IX_TenantIntegratorBillings_TenantId_IntegratorId",
                table: "TenantIntegratorBillings",
                columns: new[] { "TenantId", "IntegratorId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_TariffTiers_Integrators_IntegratorId",
                table: "TariffTiers",
                column: "IntegratorId",
                principalTable: "Integrators",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TariffTiers_Integrators_IntegratorId",
                table: "TariffTiers");

            migrationBuilder.DropTable(
                name: "ClientIntegratorBillings");

            migrationBuilder.DropTable(
                name: "TenantIntegratorBillings");

            migrationBuilder.DropIndex(
                name: "IX_TariffTiers_IntegratorId",
                table: "TariffTiers");

            migrationBuilder.DropColumn(
                name: "IntegratorId",
                table: "TariffTiers");
        }
    }
}
