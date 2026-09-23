using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RedesignPrepaidAsBag : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClientPrepaidSubscriptions");

            migrationBuilder.RenameColumn(
                name: "DurationMonths",
                table: "PrepaidPackages",
                newName: "TotalUnits");

            migrationBuilder.CreateTable(
                name: "TenantPrepaidBags",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PackageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TotalUnits = table.Column<int>(type: "int", nullable: false),
                    RemainingUnits = table.Column<decimal>(type: "decimal(9,4)", nullable: false),
                    AmountPaid = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    PurchasedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantPrepaidBags", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TenantPrepaidBags_PrepaidPackages_PackageId",
                        column: x => x.PackageId,
                        principalTable: "PrepaidPackages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TenantPrepaidBags_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TenantPrepaidBags_PackageId",
                table: "TenantPrepaidBags",
                column: "PackageId");

            migrationBuilder.CreateIndex(
                name: "IX_TenantPrepaidBags_TenantId",
                table: "TenantPrepaidBags",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TenantPrepaidBags");

            migrationBuilder.RenameColumn(
                name: "TotalUnits",
                table: "PrepaidPackages",
                newName: "DurationMonths");

            migrationBuilder.CreateTable(
                name: "ClientPrepaidSubscriptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PackageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AmountPaid = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClientPrepaidSubscriptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClientPrepaidSubscriptions_Clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ClientPrepaidSubscriptions_PrepaidPackages_PackageId",
                        column: x => x.PackageId,
                        principalTable: "PrepaidPackages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ClientPrepaidSubscriptions_ClientId",
                table: "ClientPrepaidSubscriptions",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientPrepaidSubscriptions_PackageId",
                table: "ClientPrepaidSubscriptions",
                column: "PackageId");
        }
    }
}
