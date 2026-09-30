using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLegalRepresentativeToClient : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LegalRepresentativeDocumentCountryCode",
                table: "Clients",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LegalRepresentativeDocumentNumber",
                table: "Clients",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LegalRepresentativeDocumentType",
                table: "Clients",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LegalRepresentativeEmail",
                table: "Clients",
                type: "nvarchar(320)",
                maxLength: 320,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LegalRepresentativeFirstLastName",
                table: "Clients",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LegalRepresentativeFirstName",
                table: "Clients",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LegalRepresentativeOtherNames",
                table: "Clients",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LegalRepresentativeRepresentationCode",
                table: "Clients",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LegalRepresentativeSecondLastName",
                table: "Clients",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LegalRepresentativeStartDate",
                table: "Clients",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LegalRepresentativeDocumentCountryCode",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "LegalRepresentativeDocumentNumber",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "LegalRepresentativeDocumentType",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "LegalRepresentativeEmail",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "LegalRepresentativeFirstLastName",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "LegalRepresentativeFirstName",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "LegalRepresentativeOtherNames",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "LegalRepresentativeRepresentationCode",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "LegalRepresentativeSecondLastName",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "LegalRepresentativeStartDate",
                table: "Clients");
        }
    }
}
