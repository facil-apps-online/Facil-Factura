using Fel.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fel.Infrastructure.Migrations;

[DbContext(typeof(FelDbContext))]
[Migration("20260928220000_IncreaseCertificateProfileFieldDefaultValue")]
public partial class IncreaseCertificateProfileFieldDefaultValue : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<string>(
            name: "DefaultValue",
            table: "CertificateProfileFields",
            type: "nvarchar(4000)",
            maxLength: 4000,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "nvarchar(1000)",
            oldMaxLength: 1000);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<string>(
            name: "DefaultValue",
            table: "CertificateProfileFields",
            type: "nvarchar(1000)",
            maxLength: 1000,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "nvarchar(4000)",
            oldMaxLength: 4000);
    }
}
