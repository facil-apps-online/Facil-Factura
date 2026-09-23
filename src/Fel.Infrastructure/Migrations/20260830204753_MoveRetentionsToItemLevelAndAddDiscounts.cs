using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MoveRetentionsToItemLevelAndAddDiscounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DocumentRetentions_Documents_DocumentId",
                table: "DocumentRetentions");

            migrationBuilder.RenameColumn(
                name: "DocumentId",
                table: "DocumentRetentions",
                newName: "DocumentItemId");

            migrationBuilder.RenameIndex(
                name: "IX_DocumentRetentions_DocumentId",
                table: "DocumentRetentions",
                newName: "IX_DocumentRetentions_DocumentItemId");

            migrationBuilder.AddColumn<decimal>(
                name: "GeneralDiscountAmount",
                table: "Documents",
                type: "decimal(18,4)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GeneralDiscountReason",
                table: "Documents",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PaymentTermDays",
                table: "Documents",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PurchaseOrderReference",
                table: "Documents",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DiscountRate",
                table: "DocumentItems",
                type: "decimal(5,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddForeignKey(
                name: "FK_DocumentRetentions_DocumentItems_DocumentItemId",
                table: "DocumentRetentions",
                column: "DocumentItemId",
                principalTable: "DocumentItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DocumentRetentions_DocumentItems_DocumentItemId",
                table: "DocumentRetentions");

            migrationBuilder.DropColumn(
                name: "GeneralDiscountAmount",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "GeneralDiscountReason",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "PaymentTermDays",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "PurchaseOrderReference",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "DiscountRate",
                table: "DocumentItems");

            migrationBuilder.RenameColumn(
                name: "DocumentItemId",
                table: "DocumentRetentions",
                newName: "DocumentId");

            migrationBuilder.RenameIndex(
                name: "IX_DocumentRetentions_DocumentItemId",
                table: "DocumentRetentions",
                newName: "IX_DocumentRetentions_DocumentId");

            migrationBuilder.AddForeignKey(
                name: "FK_DocumentRetentions_Documents_DocumentId",
                table: "DocumentRetentions",
                column: "DocumentId",
                principalTable: "Documents",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
