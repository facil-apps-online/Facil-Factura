using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSupportAdjustmentPrefix : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SupportAdjustmentPrefix",
                table: "Clients",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SupportAdjustmentPrefix",
                table: "Clients");
        }
    }
}
