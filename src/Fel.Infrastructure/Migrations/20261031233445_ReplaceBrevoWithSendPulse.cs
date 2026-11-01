using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ReplaceBrevoWithSendPulse : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // La key de Brevo no sirve como secreto de SendPulse: se descarta y se guardan las credenciales nuevas desde superadmin.
            migrationBuilder.DropColumn(
                name: "BrevoApiKeyEncrypted",
                table: "PlatformEmailSettings");

            migrationBuilder.AddColumn<string>(
                name: "SendPulseSecretEncrypted",
                table: "PlatformEmailSettings",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "MaxPerHour",
                table: "PlatformEmailSettings",
                type: "int",
                nullable: false,
                defaultValue: 50);

            migrationBuilder.AddColumn<string>(
                name: "SendPulseClientId",
                table: "PlatformEmailSettings",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "DeliveryDetail",
                table: "EmailOutbox",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "DeliveryStatus",
                table: "EmailOutbox",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "OpenedAt",
                table: "EmailOutbox",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "TrackedAt",
                table: "EmailOutbox",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmailOutbox_Channel_Status_SentAt",
                table: "EmailOutbox",
                columns: new[] { "Channel", "Status", "SentAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_EmailOutbox_Channel_Status_SentAt",
                table: "EmailOutbox");

            migrationBuilder.DropColumn(
                name: "MaxPerHour",
                table: "PlatformEmailSettings");

            migrationBuilder.DropColumn(
                name: "SendPulseClientId",
                table: "PlatformEmailSettings");

            migrationBuilder.DropColumn(
                name: "DeliveryDetail",
                table: "EmailOutbox");

            migrationBuilder.DropColumn(
                name: "DeliveryStatus",
                table: "EmailOutbox");

            migrationBuilder.DropColumn(
                name: "OpenedAt",
                table: "EmailOutbox");

            migrationBuilder.DropColumn(
                name: "TrackedAt",
                table: "EmailOutbox");

            migrationBuilder.DropColumn(
                name: "SendPulseSecretEncrypted",
                table: "PlatformEmailSettings");

            migrationBuilder.AddColumn<string>(
                name: "BrevoApiKeyEncrypted",
                table: "PlatformEmailSettings",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }
    }
}
