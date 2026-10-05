using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fel.Infrastructure.Migrations;

public partial class AddProductScope : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "Scope",
            table: "Products",
            type: "int",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.DropIndex(
            name: "IX_Products_ClientId_Code",
            table: "Products");

        migrationBuilder.CreateIndex(
            name: "IX_Products_ClientId_Scope_Code",
            table: "Products",
            columns: new[] { "ClientId", "Scope", "Code" },
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_Products_ClientId_Scope_Code",
            table: "Products");

        migrationBuilder.CreateIndex(
            name: "IX_Products_ClientId_Code",
            table: "Products",
            columns: new[] { "ClientId", "Code" },
            unique: true);

        migrationBuilder.DropColumn(
            name: "Scope",
            table: "Products");
    }
}
