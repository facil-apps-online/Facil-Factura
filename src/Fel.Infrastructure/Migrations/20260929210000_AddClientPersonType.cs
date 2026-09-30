using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fel.Infrastructure.Migrations
{
    public partial class AddClientPersonType : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PersonType",
                table: "Clients",
                type: "nvarchar(2)",
                maxLength: 2,
                nullable: false,
                defaultValue: "PJ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "PersonType", table: "Clients");
        }
    }
}
