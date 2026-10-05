using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AllowSameIdentificationAcrossPartyTypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Customers_ClientId_IdentificationNumber",
                table: "Customers");

            migrationBuilder.CreateIndex(
                name: "IX_Customers_ClientId_IdentificationNumber_PartyType",
                table: "Customers",
                columns: new[] { "ClientId", "IdentificationNumber", "PartyType" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Customers_ClientId_IdentificationNumber_PartyType",
                table: "Customers");

            migrationBuilder.CreateIndex(
                name: "IX_Customers_ClientId_IdentificationNumber",
                table: "Customers",
                columns: new[] { "ClientId", "IdentificationNumber" },
                unique: true);
        }
    }
}
