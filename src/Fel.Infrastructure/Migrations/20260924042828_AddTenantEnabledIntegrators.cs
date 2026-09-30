using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantEnabledIntegrators : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TenantEnabledIntegrators",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IntegratorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantEnabledIntegrators", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TenantEnabledIntegrators_Integrators_IntegratorId",
                        column: x => x.IntegratorId,
                        principalTable: "Integrators",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TenantEnabledIntegrators_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TenantEnabledIntegrators_IntegratorId",
                table: "TenantEnabledIntegrators",
                column: "IntegratorId");

            migrationBuilder.CreateIndex(
                name: "IX_TenantEnabledIntegrators_TenantId_IntegratorId",
                table: "TenantEnabledIntegrators",
                columns: new[] { "TenantId", "IntegratorId" },
                unique: true);

            // Todo Tenant existente conserva acceso a la emisión directa DIAN (lo único que ya
            // usaban) — sin esto, la migración dejaría a todos los tenants sin ningún integrador
            // habilitado y no podrían facturar hasta que Superadmin entrara a habilitarlos uno por
            // uno manualmente.
            migrationBuilder.Sql(@"
                INSERT INTO TenantEnabledIntegrators (Id, TenantId, IntegratorId)
                SELECT NEWID(), t.Id, '00000000-0000-0000-0000-000000000101'
                FROM Tenants t;
            ");

            // Dataico queda habilitado únicamente para los tenants que ya participan en una
            // jerarquía de grupo empresarial (son padre o hijo vía ParentTenantId) — según lo
            // acordado con el usuario, los dos tenants reales con esa relación (R&S y DGS) son
            // justamente los que deben mantener Dataico activo tras esta migración.
            migrationBuilder.Sql(@"
                INSERT INTO TenantEnabledIntegrators (Id, TenantId, IntegratorId)
                SELECT NEWID(), t.Id, '00000000-0000-0000-0000-000000000102'
                FROM Tenants t
                WHERE t.ParentTenantId IS NOT NULL
                   OR EXISTS (SELECT 1 FROM Tenants c WHERE c.ParentTenantId = t.Id);
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TenantEnabledIntegrators");
        }
    }
}
