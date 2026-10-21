using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPlanAlerts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ExceedAction",
                table: "TenantPlans",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ExceedActionOverride",
                table: "ClientPlanAssignments",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PlanAlerts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssignmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    BranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Documents = table.Column<int>(type: "int", nullable: false),
                    Included = table.Column<int>(type: "int", nullable: false),
                    Message = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SeenAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlanAlerts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlanAlerts_Clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TenantPlanSettings",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AlertsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    WarnPercent = table.Column<int>(type: "int", nullable: false),
                    EmailTenant = table.Column<bool>(type: "bit", nullable: false),
                    EmailClient = table.Column<bool>(type: "bit", nullable: false),
                    DefaultExceedAction = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantPlanSettings", x => x.TenantId);
                    table.ForeignKey(
                        name: "FK_TenantPlanSettings_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlanAlerts_AssignmentId_PeriodStart_Kind_BranchId",
                table: "PlanAlerts",
                columns: new[] { "AssignmentId", "PeriodStart", "Kind", "BranchId" });

            migrationBuilder.CreateIndex(
                name: "IX_PlanAlerts_ClientId",
                table: "PlanAlerts",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_PlanAlerts_TenantId_CreatedAt",
                table: "PlanAlerts",
                columns: new[] { "TenantId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlanAlerts");

            migrationBuilder.DropTable(
                name: "TenantPlanSettings");

            migrationBuilder.DropColumn(
                name: "ExceedAction",
                table: "TenantPlans");

            migrationBuilder.DropColumn(
                name: "ExceedActionOverride",
                table: "ClientPlanAssignments");
        }
    }
}
