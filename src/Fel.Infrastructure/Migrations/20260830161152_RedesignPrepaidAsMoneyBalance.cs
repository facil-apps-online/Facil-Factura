using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RedesignPrepaidAsMoneyBalance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RemainingUnits",
                table: "TenantPrepaidBags");

            migrationBuilder.DropColumn(
                name: "TotalUnits",
                table: "TenantPrepaidBags");

            migrationBuilder.DropColumn(
                name: "TotalUnits",
                table: "PrepaidPackages");

            migrationBuilder.AddColumn<decimal>(
                name: "DiscountedPricePerUser",
                table: "TenantPrepaidBags",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "RemainingBalance",
                table: "TenantPrepaidBags",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "DiscountedPricePerUser",
                table: "PrepaidPackages",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DiscountedPricePerUser",
                table: "TenantPrepaidBags");

            migrationBuilder.DropColumn(
                name: "RemainingBalance",
                table: "TenantPrepaidBags");

            migrationBuilder.DropColumn(
                name: "DiscountedPricePerUser",
                table: "PrepaidPackages");

            migrationBuilder.AddColumn<decimal>(
                name: "RemainingUnits",
                table: "TenantPrepaidBags",
                type: "decimal(9,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "TotalUnits",
                table: "TenantPrepaidBags",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TotalUnits",
                table: "PrepaidPackages",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }
    }
}
