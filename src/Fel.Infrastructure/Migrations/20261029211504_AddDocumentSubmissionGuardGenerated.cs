using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentSubmissionGuardGenerated : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SubmissionAttemptCount",
                table: "Documents",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "SubmissionLastError",
                table: "Documents",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SubmissionLeaseUntil",
                table: "Documents",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SubmissionOperationId",
                table: "Documents",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SubmissionStartedAt",
                table: "Documents",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SubmissionState",
                table: "Documents",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SubmissionAttemptCount",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "SubmissionLastError",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "SubmissionLeaseUntil",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "SubmissionOperationId",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "SubmissionStartedAt",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "SubmissionState",
                table: "Documents");
        }
    }
}
