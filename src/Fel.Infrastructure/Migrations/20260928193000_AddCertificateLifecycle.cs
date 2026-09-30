using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Fel.Infrastructure.Data;

#nullable disable

namespace Fel.Infrastructure.Migrations;

[DbContext(typeof(FelDbContext))]
[Migration("20260928193000_AddCertificateLifecycle")]
public partial class AddCertificateLifecycle : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>("Environment", "Certificates", nullable: false, defaultValue: 2);
        migrationBuilder.AddColumn<Guid>("ProviderId", "Certificates", nullable: true);
        migrationBuilder.AddColumn<Guid>("ProfileId", "Certificates", nullable: true);
        migrationBuilder.AddColumn<Guid>("CertificateRequestId", "Certificates", nullable: true);
        migrationBuilder.AddColumn<string>("Thumbprint", "Certificates", maxLength: 128, nullable: false, defaultValue: "");
        migrationBuilder.AddColumn<string>("SerialNumber", "Certificates", maxLength: 128, nullable: false, defaultValue: "");
        migrationBuilder.AddColumn<string>("Subject", "Certificates", maxLength: 1000, nullable: false, defaultValue: "");
        migrationBuilder.AddColumn<string>("Issuer", "Certificates", maxLength: 1000, nullable: false, defaultValue: "");
        migrationBuilder.AddColumn<DateTime>("NotBefore", "Certificates", nullable: true);
        migrationBuilder.AddColumn<DateTime>("NotAfter", "Certificates", nullable: true);
        migrationBuilder.AddColumn<DateTime>("ActivationAt", "Certificates", nullable: true);
        migrationBuilder.AddColumn<int>("Status", "Certificates", nullable: false, defaultValue: 1);
        migrationBuilder.AddColumn<DateTime>("ActivatedAt", "Certificates", nullable: true);
        migrationBuilder.AddColumn<DateTime>("RetiredAt", "Certificates", nullable: true);
        migrationBuilder.AddColumn<DateTime>("RevokedAt", "Certificates", nullable: true);
        migrationBuilder.AddColumn<bool>("AutoRenewalEnabled", "Certificates", nullable: false, defaultValue: false);
        migrationBuilder.AddColumn<int>("RenewalLeadTimeDays", "Certificates", nullable: true);

        migrationBuilder.Sql("UPDATE Certificates SET NotBefore = CreatedAt, NotAfter = ExpirationDate WHERE NotBefore IS NULL OR NotAfter IS NULL");
        migrationBuilder.AlterColumn<DateTime>("NotBefore", "Certificates", nullable: false, defaultValueSql: "GETUTCDATE()", oldNullable: true);
        migrationBuilder.AlterColumn<DateTime>("NotAfter", "Certificates", nullable: false, defaultValueSql: "GETUTCDATE()", oldNullable: true);

        migrationBuilder.CreateTable("CertificateProviders", table => new
        {
            Id = table.Column<Guid>("uniqueidentifier", nullable: false),
            Key = table.Column<string>("nvarchar(80)", maxLength: 80, nullable: false),
            Name = table.Column<string>("nvarchar(150)", maxLength: 150, nullable: false),
            IsActive = table.Column<bool>("bit", nullable: false),
            SandboxBaseUrl = table.Column<string>("nvarchar(500)", maxLength: 500, nullable: false),
            ProductionBaseUrl = table.Column<string>("nvarchar(500)", maxLength: 500, nullable: false),
            DownloadBaseUrl = table.Column<string>("nvarchar(500)", maxLength: 500, nullable: false),
            RaCode = table.Column<string>("nvarchar(100)", maxLength: 100, nullable: false),
            ConsumerKeySecretName = table.Column<string>("nvarchar(200)", maxLength: 200, nullable: false),
            ConsumerSecretSecretName = table.Column<string>("nvarchar(200)", maxLength: 200, nullable: false),
            RequestTimeoutSeconds = table.Column<int>("int", nullable: false),
            MaxRetryAttempts = table.Column<int>("int", nullable: false),
            CreatedAt = table.Column<DateTime>("datetime2", nullable: false),
            UpdatedAt = table.Column<DateTime>("datetime2", nullable: false)
        }, constraints: table => table.PrimaryKey("PK_CertificateProviders", x => x.Id));

        migrationBuilder.CreateTable("CertificateProfiles", table => new
        {
            Id = table.Column<Guid>("uniqueidentifier", nullable: false),
            ProviderId = table.Column<Guid>("uniqueidentifier", nullable: false),
            Environment = table.Column<int>("int", nullable: false),
            ExternalCode = table.Column<string>("nvarchar(500)", maxLength: 500, nullable: false),
            Key = table.Column<string>("nvarchar(100)", maxLength: 100, nullable: false),
            Title = table.Column<string>("nvarchar(250)", maxLength: 250, nullable: false),
            Description = table.Column<string>("nvarchar(1000)", maxLength: 1000, nullable: false),
            PersonType = table.Column<string>("nvarchar(50)", maxLength: 50, nullable: false),
            ExternalType = table.Column<string>("nvarchar(100)", maxLength: 100, nullable: false),
            TokenType = table.Column<string>("nvarchar(50)", maxLength: 50, nullable: false),
            ProviderValidityDays = table.Column<int>("int", nullable: true),
            TermsUrl = table.Column<string>("nvarchar(1000)", maxLength: 1000, nullable: false),
            TermsHash = table.Column<string>("nvarchar(128)", maxLength: 128, nullable: false),
            IsActive = table.Column<bool>("bit", nullable: false),
            LastSyncedAt = table.Column<DateTime>("datetime2", nullable: true),
            CreatedAt = table.Column<DateTime>("datetime2", nullable: false),
            UpdatedAt = table.Column<DateTime>("datetime2", nullable: false)
        }, constraints: table =>
        {
            table.PrimaryKey("PK_CertificateProfiles", x => x.Id);
            table.ForeignKey("FK_CertificateProfiles_CertificateProviders_ProviderId", x => x.ProviderId, "CertificateProviders", "Id", onDelete: ReferentialAction.Restrict);
        });

        migrationBuilder.CreateTable("CertificateRequests", table => new
        {
            Id = table.Column<Guid>("uniqueidentifier", nullable: false),
            TenantId = table.Column<Guid>("uniqueidentifier", nullable: false),
            ClientId = table.Column<Guid>("uniqueidentifier", nullable: false),
            ProfileId = table.Column<Guid>("uniqueidentifier", nullable: false),
            Environment = table.Column<int>("int", nullable: false),
            RequestType = table.Column<int>("int", nullable: false),
            PreviousCertificateId = table.Column<Guid>("uniqueidentifier", nullable: true),
            ProviderRequestCode = table.Column<string>("nvarchar(100)", maxLength: 100, nullable: false),
            ProviderPublicId = table.Column<string>("nvarchar(200)", maxLength: 200, nullable: false),
            ProviderStatus = table.Column<string>("nvarchar(100)", maxLength: 100, nullable: false),
            Status = table.Column<int>("int", nullable: false),
            AdvancedAccreditedStatus = table.Column<string>("nvarchar(50)", maxLength: 50, nullable: false),
            AdvancedPaymentStatus = table.Column<string>("nvarchar(50)", maxLength: 50, nullable: false),
            KycUrl = table.Column<string>("nvarchar(2000)", maxLength: 2000, nullable: false),
            KycUrlFetchedAt = table.Column<DateTime>("datetime2", nullable: true),
            KycCompletedAt = table.Column<DateTime>("datetime2", nullable: true),
            CsrReference = table.Column<string>("nvarchar(500)", maxLength: 500, nullable: false),
            CsrHash = table.Column<string>("nvarchar(128)", maxLength: 128, nullable: false),
            PublicKeyHash = table.Column<string>("nvarchar(128)", maxLength: 128, nullable: false),
            EncryptedPrivateKey = table.Column<string>("nvarchar(max)", nullable: false),
            KeyAlgorithm = table.Column<string>("nvarchar(30)", maxLength: 30, nullable: false),
            KeySize = table.Column<int>("int", nullable: false),
            TermsUrl = table.Column<string>("nvarchar(1000)", maxLength: 1000, nullable: false),
            TermsHash = table.Column<string>("nvarchar(128)", maxLength: 128, nullable: false),
            TermsAcceptedAt = table.Column<DateTime>("datetime2", nullable: true),
            TermsAcceptedByUserId = table.Column<Guid>("uniqueidentifier", nullable: true),
            TermsAcceptedIpAddress = table.Column<string>("nvarchar(80)", maxLength: 80, nullable: false),
            TermsAcceptedUserAgent = table.Column<string>("nvarchar(1000)", maxLength: 1000, nullable: false),
            SubmittedAt = table.Column<DateTime>("datetime2", nullable: true), ReadyAt = table.Column<DateTime>("datetime2", nullable: true),
            DownloadedAt = table.Column<DateTime>("datetime2", nullable: true), InstalledAt = table.Column<DateTime>("datetime2", nullable: true),
            CompletedAt = table.Column<DateTime>("datetime2", nullable: true), LastProviderSyncAt = table.Column<DateTime>("datetime2", nullable: true),
            NextProviderSyncAt = table.Column<DateTime>("datetime2", nullable: true), RetryCount = table.Column<int>("int", nullable: false),
            NextRetryAt = table.Column<DateTime>("datetime2", nullable: true), LastErrorCode = table.Column<string>("nvarchar(150)", maxLength: 150, nullable: false),
            LastErrorMessage = table.Column<string>("nvarchar(2000)", maxLength: 2000, nullable: false), IdempotencyKey = table.Column<string>("nvarchar(150)", maxLength: 150, nullable: false),
            CreatedByUserId = table.Column<Guid>("uniqueidentifier", nullable: true), CreatedAt = table.Column<DateTime>("datetime2", nullable: false), UpdatedAt = table.Column<DateTime>("datetime2", nullable: false)
        }, constraints: table =>
        {
            table.PrimaryKey("PK_CertificateRequests", x => x.Id);
            table.ForeignKey("FK_CertificateRequests_Tenants_TenantId", x => x.TenantId, "Tenants", "Id", onDelete: ReferentialAction.Restrict);
            table.ForeignKey("FK_CertificateRequests_Clients_ClientId", x => x.ClientId, "Clients", "Id", onDelete: ReferentialAction.Restrict);
            table.ForeignKey("FK_CertificateRequests_CertificateProfiles_ProfileId", x => x.ProfileId, "CertificateProfiles", "Id", onDelete: ReferentialAction.Restrict);
            table.ForeignKey("FK_CertificateRequests_Certificates_PreviousCertificateId", x => x.PreviousCertificateId, "Certificates", "Id", onDelete: ReferentialAction.Restrict);
        });

        migrationBuilder.CreateTable("CertificateProfileFields", table => new
        {
            Id = table.Column<Guid>("uniqueidentifier", nullable: false), ProfileId = table.Column<Guid>("uniqueidentifier", nullable: false),
            ExternalName = table.Column<string>("nvarchar(150)", maxLength: 150, nullable: false), Label = table.Column<string>("nvarchar(300)", maxLength: 300, nullable: false),
            Type = table.Column<string>("nvarchar(50)", maxLength: 50, nullable: false), ValidationPattern = table.Column<string>("nvarchar(2000)", maxLength: 2000, nullable: false),
            DefaultValue = table.Column<string>("nvarchar(1000)", maxLength: 1000, nullable: false), IsRequired = table.Column<bool>("bit", nullable: false),
            IsEditable = table.Column<bool>("bit", nullable: false), IsUsed = table.Column<bool>("bit", nullable: false), DisplayOrder = table.Column<int>("int", nullable: false),
            DefinitionHash = table.Column<string>("nvarchar(128)", maxLength: 128, nullable: false), FetchedAt = table.Column<DateTime>("datetime2", nullable: false)
        }, constraints: table =>
        {
            table.PrimaryKey("PK_CertificateProfileFields", x => x.Id);
            table.ForeignKey("FK_CertificateProfileFields_CertificateProfiles_ProfileId", x => x.ProfileId, "CertificateProfiles", "Id", onDelete: ReferentialAction.Cascade);
        });

        migrationBuilder.CreateTable("CertificatePrices", table => new
        {
            Id = table.Column<Guid>("uniqueidentifier", nullable: false), ProviderId = table.Column<Guid>("uniqueidentifier", nullable: false), ProfileId = table.Column<Guid>("uniqueidentifier", nullable: false),
            Environment = table.Column<int>("int", nullable: false), TenantId = table.Column<Guid>("uniqueidentifier", nullable: true), PriceType = table.Column<string>("nvarchar(50)", maxLength: 50, nullable: false),
            Currency = table.Column<string>("nvarchar(10)", maxLength: 10, nullable: false), NetAmount = table.Column<decimal>("decimal(18,2)", nullable: false), TaxRate = table.Column<decimal>("decimal(8,4)", nullable: false),
            EffectiveFrom = table.Column<DateTime>("datetime2", nullable: false), EffectiveTo = table.Column<DateTime>("datetime2", nullable: true), IsActive = table.Column<bool>("bit", nullable: false),
            CreatedByUserId = table.Column<Guid>("uniqueidentifier", nullable: true), CreatedAt = table.Column<DateTime>("datetime2", nullable: false)
        }, constraints: table =>
        {
            table.PrimaryKey("PK_CertificatePrices", x => x.Id);
            table.ForeignKey("FK_CertificatePrices_CertificateProviders_ProviderId", x => x.ProviderId, "CertificateProviders", "Id", onDelete: ReferentialAction.Restrict);
            table.ForeignKey("FK_CertificatePrices_CertificateProfiles_ProfileId", x => x.ProfileId, "CertificateProfiles", "Id", onDelete: ReferentialAction.Restrict);
            table.ForeignKey("FK_CertificatePrices_Tenants_TenantId", x => x.TenantId, "Tenants", "Id", onDelete: ReferentialAction.Restrict);
        });

        migrationBuilder.CreateTable("CertificateCharges", table => new
        {
            Id = table.Column<Guid>("uniqueidentifier", nullable: false), TenantId = table.Column<Guid>("uniqueidentifier", nullable: false), ClientId = table.Column<Guid>("uniqueidentifier", nullable: false),
            CertificateRequestId = table.Column<Guid>("uniqueidentifier", nullable: false), PriceId = table.Column<Guid>("uniqueidentifier", nullable: false), ChargeType = table.Column<int>("int", nullable: false),
            Status = table.Column<int>("int", nullable: false), UnitPrice = table.Column<decimal>("decimal(18,2)", nullable: false), TaxAmount = table.Column<decimal>("decimal(18,2)", nullable: false), TotalAmount = table.Column<decimal>("decimal(18,2)", nullable: false),
            Currency = table.Column<string>("nvarchar(10)", maxLength: 10, nullable: false), BillingPeriodStart = table.Column<DateTime>("datetime2", nullable: false), BillingPeriodEnd = table.Column<DateTime>("datetime2", nullable: false),
            OccurredAt = table.Column<DateTime>("datetime2", nullable: false), DescriptionSnapshot = table.Column<string>("nvarchar(500)", maxLength: 500, nullable: false), CreatedAt = table.Column<DateTime>("datetime2", nullable: false)
        }, constraints: table =>
        {
            table.PrimaryKey("PK_CertificateCharges", x => x.Id);
            table.ForeignKey("FK_CertificateCharges_Tenants_TenantId", x => x.TenantId, "Tenants", "Id", onDelete: ReferentialAction.Restrict);
            table.ForeignKey("FK_CertificateCharges_Clients_ClientId", x => x.ClientId, "Clients", "Id", onDelete: ReferentialAction.Restrict);
            table.ForeignKey("FK_CertificateCharges_CertificateRequests_CertificateRequestId", x => x.CertificateRequestId, "CertificateRequests", "Id", onDelete: ReferentialAction.Restrict);
            table.ForeignKey("FK_CertificateCharges_CertificatePrices_PriceId", x => x.PriceId, "CertificatePrices", "Id", onDelete: ReferentialAction.Restrict);
        });

        migrationBuilder.CreateTable("CertificateEvents", table => new
        {
            Id = table.Column<Guid>("uniqueidentifier", nullable: false), CertificateRequestId = table.Column<Guid>("uniqueidentifier", nullable: false), CertificateId = table.Column<Guid>("uniqueidentifier", nullable: true),
            EventType = table.Column<int>("int", nullable: false), FromStatus = table.Column<int>("int", nullable: true), ToStatus = table.Column<int>("int", nullable: true), ProviderStatus = table.Column<string>("nvarchar(100)", maxLength: 100, nullable: false),
            ProviderHttpStatus = table.Column<int>("int", nullable: true), CorrelationId = table.Column<string>("nvarchar(100)", maxLength: 100, nullable: false), IdempotencyKey = table.Column<string>("nvarchar(150)", maxLength: 150, nullable: false),
            ActorType = table.Column<string>("nvarchar(50)", maxLength: 50, nullable: false), ActorId = table.Column<Guid>("uniqueidentifier", nullable: true), OccurredAt = table.Column<DateTime>("datetime2", nullable: false), MetadataJson = table.Column<string>("nvarchar(max)", nullable: false)
        }, constraints: table =>
        {
            table.PrimaryKey("PK_CertificateEvents", x => x.Id);
            table.ForeignKey("FK_CertificateEvents_CertificateRequests_CertificateRequestId", x => x.CertificateRequestId, "CertificateRequests", "Id", onDelete: ReferentialAction.Cascade);
            table.ForeignKey("FK_CertificateEvents_Certificates_CertificateId", x => x.CertificateId, "Certificates", "Id", onDelete: ReferentialAction.Restrict);
        });

        migrationBuilder.CreateIndex("IX_Certificates_ProviderId", "Certificates", "ProviderId");
        migrationBuilder.CreateIndex("IX_Certificates_ProfileId", "Certificates", "ProfileId");
        migrationBuilder.CreateIndex("IX_Certificates_CertificateRequestId", "Certificates", "CertificateRequestId", unique: true, filter: "[CertificateRequestId] IS NOT NULL");
        migrationBuilder.CreateIndex("IX_Certificates_Thumbprint", "Certificates", "Thumbprint", unique: true, filter: "[Thumbprint] <> ''");
        migrationBuilder.CreateIndex("IX_Certificates_ClientId_Environment_Status", "Certificates", new[] { "ClientId", "Environment", "Status" });
        migrationBuilder.CreateIndex("IX_CertificateProviders_Key", "CertificateProviders", "Key", unique: true);
        migrationBuilder.CreateIndex("IX_CertificateProfiles_ProviderId_Environment_ExternalCode", "CertificateProfiles", new[] { "ProviderId", "Environment", "ExternalCode" }, unique: true);
        migrationBuilder.CreateIndex("IX_CertificateProfiles_ProviderId", "CertificateProfiles", "ProviderId");
        migrationBuilder.CreateIndex("IX_CertificateProfileFields_ProfileId_ExternalName", "CertificateProfileFields", new[] { "ProfileId", "ExternalName" }, unique: true);
        migrationBuilder.CreateIndex("IX_CertificateRequests_IdempotencyKey", "CertificateRequests", "IdempotencyKey", unique: true);
        migrationBuilder.CreateIndex("IX_CertificateRequests_ClientId_Environment_Status", "CertificateRequests", new[] { "ClientId", "Environment", "Status" });
        migrationBuilder.CreateIndex("IX_CertificateRequests_ProviderRequestCode_Environment", "CertificateRequests", new[] { "ProviderRequestCode", "Environment" }, unique: true, filter: "[ProviderRequestCode] <> ''");
        migrationBuilder.CreateIndex("IX_CertificateRequests_TenantId", "CertificateRequests", "TenantId");
        migrationBuilder.CreateIndex("IX_CertificateRequests_ProfileId", "CertificateRequests", "ProfileId");
        migrationBuilder.CreateIndex("IX_CertificateRequests_PreviousCertificateId", "CertificateRequests", "PreviousCertificateId");
        migrationBuilder.CreateIndex("IX_CertificateProfileFields_ProfileId", "CertificateProfileFields", "ProfileId");
        migrationBuilder.CreateIndex("IX_CertificatePrices_ProviderId_ProfileId_Environment_TenantId_EffectiveFrom", "CertificatePrices", new[] { "ProviderId", "ProfileId", "Environment", "TenantId", "EffectiveFrom" });
        migrationBuilder.CreateIndex("IX_CertificatePrices_ProviderId", "CertificatePrices", "ProviderId");
        migrationBuilder.CreateIndex("IX_CertificatePrices_ProfileId", "CertificatePrices", "ProfileId");
        migrationBuilder.CreateIndex("IX_CertificatePrices_TenantId", "CertificatePrices", "TenantId");
        migrationBuilder.CreateIndex("IX_CertificateCharges_CertificateRequestId_ChargeType", "CertificateCharges", new[] { "CertificateRequestId", "ChargeType" }, unique: true, filter: "[Status] <> 4");
        migrationBuilder.CreateIndex("IX_CertificateCharges_TenantId", "CertificateCharges", "TenantId");
        migrationBuilder.CreateIndex("IX_CertificateCharges_ClientId", "CertificateCharges", "ClientId");
        migrationBuilder.CreateIndex("IX_CertificateCharges_PriceId", "CertificateCharges", "PriceId");
        migrationBuilder.CreateIndex("IX_CertificateEvents_CertificateRequestId_OccurredAt", "CertificateEvents", new[] { "CertificateRequestId", "OccurredAt" });
        migrationBuilder.CreateIndex("IX_CertificateEvents_CertificateId", "CertificateEvents", "CertificateId");

        migrationBuilder.AddForeignKey(
            name: "FK_Certificates_CertificateProviders_ProviderId",
            table: "Certificates",
            column: "ProviderId",
            principalTable: "CertificateProviders",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);
        migrationBuilder.AddForeignKey(
            name: "FK_Certificates_CertificateProfiles_ProfileId",
            table: "Certificates",
            column: "ProfileId",
            principalTable: "CertificateProfiles",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);
        migrationBuilder.AddForeignKey(
            name: "FK_Certificates_CertificateRequests_CertificateRequestId",
            table: "Certificates",
            column: "CertificateRequestId",
            principalTable: "CertificateRequests",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey("FK_Certificates_CertificateRequests_CertificateRequestId", "Certificates");
        migrationBuilder.DropForeignKey("FK_Certificates_CertificateProfiles_ProfileId", "Certificates");
        migrationBuilder.DropForeignKey("FK_Certificates_CertificateProviders_ProviderId", "Certificates");
        migrationBuilder.DropTable("CertificateEvents");
        migrationBuilder.DropTable("CertificateCharges");
        migrationBuilder.DropTable("CertificatePrices");
        migrationBuilder.DropTable("CertificateProfileFields");
        migrationBuilder.DropTable("CertificateRequests");
        migrationBuilder.DropTable("CertificateProfiles");
        migrationBuilder.DropTable("CertificateProviders");
        foreach (var column in new[] { "Environment", "ProviderId", "ProfileId", "CertificateRequestId", "Thumbprint", "SerialNumber", "Subject", "Issuer", "NotBefore", "NotAfter", "ActivationAt", "Status", "ActivatedAt", "RetiredAt", "RevokedAt", "AutoRenewalEnabled", "RenewalLeadTimeDays" })
            migrationBuilder.DropColumn(column, "Certificates");
    }
}
