using Microsoft.EntityFrameworkCore.Migrations;
using Fel.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;

#nullable disable

namespace Fel.Infrastructure.Migrations;

/// <inheritdoc />
[DbContext(typeof(FelDbContext))]
[Migration("20260930123000_ExpandInvoiceItemDescription")]
public partial class ExpandInvoiceItemDescription : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<string>(
            name: "Name",
            table: "DocumentItems",
            type: "nvarchar(max)",
            nullable: false,
            oldClrType: typeof(string),
            oldType: "nvarchar(250)",
            oldMaxLength: 250);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<string>(
            name: "Name",
            table: "DocumentItems",
            type: "nvarchar(250)",
            maxLength: 250,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "nvarchar(max)");
    }
}
