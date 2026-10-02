using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fel.Infrastructure.Migrations
{
    public partial class AddClientDecimalSeparator : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // "." = punto decimal y coma de miles: el estándar y el valor para los clientes que ya existen.
            migrationBuilder.AddColumn<string>(
                name: "DecimalSeparator",
                table: "Clients",
                type: "nvarchar(1)",
                maxLength: 1,
                nullable: false,
                defaultValue: ".");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "DecimalSeparator", table: "Clients");
        }
    }
}
