using Fel.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fel.Infrastructure.Migrations;

[DbContext(typeof(FelDbContext))]
[Migration("20260928213000_AddCertificateSandboxEnablement")]
public partial class AddCertificateSandboxEnablement : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>("SandboxEnabled", "CertificateProviders", nullable: false, defaultValue: false);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn("SandboxEnabled", "CertificateProviders");
    }
}
