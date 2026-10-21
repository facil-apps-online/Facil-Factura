using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOwnerCustomerSync : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AutoSyncCustomers",
                table: "TenantInvoicingSettings",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "LinkedClientId",
                table: "Customers",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Customers_ClientId_LinkedClientId",
                table: "Customers",
                columns: new[] { "ClientId", "LinkedClientId" },
                unique: true,
                filter: "[LinkedClientId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Customers_ClientId_LinkedClientId",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "AutoSyncCustomers",
                table: "TenantInvoicingSettings");

            migrationBuilder.DropColumn(
                name: "LinkedClientId",
                table: "Customers");
        }
    }
}
