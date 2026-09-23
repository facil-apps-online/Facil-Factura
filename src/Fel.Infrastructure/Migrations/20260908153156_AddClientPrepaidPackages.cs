using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddClientPrepaidPackages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ClientPrepaidPackages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IntegratorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    TotalPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DiscountedPricePerDocument = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClientPrepaidPackages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClientPrepaidPackages_Clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ClientPrepaidPackages_Integrators_IntegratorId",
                        column: x => x.IntegratorId,
                        principalTable: "Integrators",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ClientPrepaidBags",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PackageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IntegratorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AmountPaid = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    RemainingBalance = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DiscountedPricePerDocument = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    PurchasedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClientPrepaidBags", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClientPrepaidBags_ClientPrepaidPackages_PackageId",
                        column: x => x.PackageId,
                        principalTable: "ClientPrepaidPackages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClientPrepaidBags_Clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ClientPrepaidBags_Integrators_IntegratorId",
                        column: x => x.IntegratorId,
                        principalTable: "Integrators",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ClientPrepaidBags_ClientId",
                table: "ClientPrepaidBags",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientPrepaidBags_IntegratorId",
                table: "ClientPrepaidBags",
                column: "IntegratorId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientPrepaidBags_PackageId",
                table: "ClientPrepaidBags",
                column: "PackageId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientPrepaidPackages_ClientId",
                table: "ClientPrepaidPackages",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientPrepaidPackages_IntegratorId",
                table: "ClientPrepaidPackages",
                column: "IntegratorId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClientPrepaidBags");

            migrationBuilder.DropTable(
                name: "ClientPrepaidPackages");
        }
    }
}
