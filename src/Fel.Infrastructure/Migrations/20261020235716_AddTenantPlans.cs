using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantPlans : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TenantPlans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Cycle = table.Column<int>(type: "int", nullable: false),
                    FixedFee = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    FeeTiming = table.Column<int>(type: "int", nullable: false),
                    IncludedDocuments = table.Column<int>(type: "int", nullable: true),
                    PricePerDocument = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Scope = table.Column<int>(type: "int", nullable: false),
                    CountedStatuses = table.Column<int>(type: "int", nullable: false),
                    CountedTypes = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantPlans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TenantPlans_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ClientPlanAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlanId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FixedFeeOverride = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    PricePerDocumentOverride = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClientPlanAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClientPlanAssignments_Clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ClientPlanAssignments_TenantPlans_PlanId",
                        column: x => x.PlanId,
                        principalTable: "TenantPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ClientPlanAssignments_ClientId_StartDate",
                table: "ClientPlanAssignments",
                columns: new[] { "ClientId", "StartDate" });

            migrationBuilder.CreateIndex(
                name: "IX_ClientPlanAssignments_PlanId",
                table: "ClientPlanAssignments",
                column: "PlanId");

            migrationBuilder.CreateIndex(
                name: "IX_TenantPlans_TenantId_Name",
                table: "TenantPlans",
                columns: new[] { "TenantId", "Name" },
                unique: true);

            // La tarifa que hoy tiene cada cliente en su sucursal principal (cuota mensual y precio por documento) pasa a ser un plan asignado, sin
            // cambiar lo que se cobra: por sucursal, ciclo mensual, cuota por adelantado, sin documentos incluidos, cuenta solo los aprobados y todos
            // los tipos. Solo los clientes con alguna tarifa distinta de cero; los de prueba de developers quedan fuera. Idempotente.
            migrationBuilder.Sql(@"
INSERT INTO TenantPlans (Id, TenantId, Name, Description, Cycle, FixedFee, FeeTiming, IncludedDocuments, PricePerDocument, Scope, CountedStatuses, CountedTypes, IsActive, CreatedAt, UpdatedAt)
SELECT NEWID(), x.TenantId,
       N'Tarifa actual: cuota ' + CONVERT(NVARCHAR(30), x.SubscriptionRate) + N' y documento ' + CONVERT(NVARCHAR(30), x.PricePerDocument),
       N'Creado a partir de la tarifa que tenía cada sucursal principal.',
       0, x.SubscriptionRate, 0, 0, x.PricePerDocument, 1, 1, 15, 1, SYSUTCDATETIME(), SYSUTCDATETIME()
FROM (SELECT DISTINCT c.TenantId, b.SubscriptionRate, b.PricePerDocument
      FROM Branches b JOIN Clients c ON c.Id = b.ClientId
      WHERE b.IsMain = 1 AND c.IsDeveloperSandbox = 0 AND (b.SubscriptionRate <> 0 OR b.PricePerDocument <> 0)) x
WHERE NOT EXISTS (SELECT 1 FROM TenantPlans p WHERE p.TenantId = x.TenantId
                  AND p.Name = N'Tarifa actual: cuota ' + CONVERT(NVARCHAR(30), x.SubscriptionRate) + N' y documento ' + CONVERT(NVARCHAR(30), x.PricePerDocument));

INSERT INTO ClientPlanAssignments (Id, ClientId, PlanId, StartDate, EndDate, FixedFeeOverride, PricePerDocumentOverride, CreatedAt)
SELECT NEWID(), c.Id, p.Id, CONVERT(DATE, c.CreatedAt), NULL, NULL, NULL, SYSUTCDATETIME()
FROM Clients c
JOIN Branches b ON b.ClientId = c.Id AND b.IsMain = 1
JOIN TenantPlans p ON p.TenantId = c.TenantId
     AND p.Name = N'Tarifa actual: cuota ' + CONVERT(NVARCHAR(30), b.SubscriptionRate) + N' y documento ' + CONVERT(NVARCHAR(30), b.PricePerDocument)
WHERE c.IsDeveloperSandbox = 0
  AND NOT EXISTS (SELECT 1 FROM ClientPlanAssignments a WHERE a.ClientId = c.Id);
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClientPlanAssignments");

            migrationBuilder.DropTable(
                name: "TenantPlans");
        }
    }
}
