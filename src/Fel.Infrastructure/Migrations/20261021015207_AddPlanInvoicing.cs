using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPlanInvoicing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ExtraProductId",
                table: "TenantPlans",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "FeeProductId",
                table: "TenantPlans",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "InvoicingMode",
                table: "ClientPlanAssignments",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "PlanCharges",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssignmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlanId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlanName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RunPeriod = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FeeAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ExtraAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    DocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlanCharges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlanCharges_Clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TenantInvoicingSettings",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerClientId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    GenerateDrafts = table.Column<bool>(type: "bit", nullable: false),
                    DraftDay = table.Column<int>(type: "int", nullable: false),
                    FeeProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ExtraProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantInvoicingSettings", x => x.TenantId);
                    table.ForeignKey(
                        name: "FK_TenantInvoicingSettings_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlanCharges_ClientId_RunPeriod",
                table: "PlanCharges",
                columns: new[] { "ClientId", "RunPeriod" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlanCharges_TenantId_RunPeriod",
                table: "PlanCharges",
                columns: new[] { "TenantId", "RunPeriod" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlanCharges");

            migrationBuilder.DropTable(
                name: "TenantInvoicingSettings");

            migrationBuilder.DropColumn(
                name: "ExtraProductId",
                table: "TenantPlans");

            migrationBuilder.DropColumn(
                name: "FeeProductId",
                table: "TenantPlans");

            migrationBuilder.DropColumn(
                name: "InvoicingMode",
                table: "ClientPlanAssignments");
        }
    }
}
