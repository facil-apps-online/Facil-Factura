using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Fel.Infrastructure.Data;

#nullable disable

namespace Fel.Infrastructure.Migrations;

[DbContext(typeof(FelDbContext))]
[Migration("20260928210000_AddEncryptedCertificateProviderCredentials")]
public partial class AddEncryptedCertificateProviderCredentials : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>("EncryptedSandboxConsumerKey", "CertificateProviders", maxLength: 4000, nullable: false, defaultValue: "");
        migrationBuilder.AddColumn<string>("EncryptedSandboxConsumerSecret", "CertificateProviders", maxLength: 4000, nullable: false, defaultValue: "");
        migrationBuilder.AddColumn<string>("EncryptedProductionConsumerKey", "CertificateProviders", maxLength: 4000, nullable: false, defaultValue: "");
        migrationBuilder.AddColumn<string>("EncryptedProductionConsumerSecret", "CertificateProviders", maxLength: 4000, nullable: false, defaultValue: "");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn("EncryptedSandboxConsumerKey", "CertificateProviders");
        migrationBuilder.DropColumn("EncryptedSandboxConsumerSecret", "CertificateProviders");
        migrationBuilder.DropColumn("EncryptedProductionConsumerKey", "CertificateProviders");
        migrationBuilder.DropColumn("EncryptedProductionConsumerSecret", "CertificateProviders");
    }
}
