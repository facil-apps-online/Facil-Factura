using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddReceptionEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AutoSendAceptacion",
                table: "Clients",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "AutoSendAcuseRecibo",
                table: "Clients",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "AutoSendReciboBien",
                table: "Clients",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "AutoSendReclamo",
                table: "Clients",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ReceptionEmailEnabled",
                table: "Clients",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ReceptionEmailHost",
                table: "Clients",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ReceptionEmailPasswordEncrypted",
                table: "Clients",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "ReceptionEmailPort",
                table: "Clients",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "ReceptionEmailUseSsl",
                table: "Clients",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ReceptionEmailUser",
                table: "Clients",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "ReceivedDocuments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RawXml = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Cufe = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DocumentTypeCode = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IssuerTaxId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IssuerName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DocumentId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IssueDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ReceivedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReceivedDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReceivedDocuments_Clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ReceivedDocumentEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReceivedDocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventCode = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Cude = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DianResponseMessage = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SentAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReceivedDocumentEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReceivedDocumentEvents_ReceivedDocuments_ReceivedDocumentId",
                        column: x => x.ReceivedDocumentId,
                        principalTable: "ReceivedDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReceivedDocumentEvents_ReceivedDocumentId",
                table: "ReceivedDocumentEvents",
                column: "ReceivedDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_ReceivedDocuments_ClientId",
                table: "ReceivedDocuments",
                column: "ClientId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReceivedDocumentEvents");

            migrationBuilder.DropTable(
                name: "ReceivedDocuments");

            migrationBuilder.DropColumn(
                name: "AutoSendAceptacion",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "AutoSendAcuseRecibo",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "AutoSendReciboBien",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "AutoSendReclamo",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "ReceptionEmailEnabled",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "ReceptionEmailHost",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "ReceptionEmailPasswordEncrypted",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "ReceptionEmailPort",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "ReceptionEmailUseSsl",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "ReceptionEmailUser",
                table: "Clients");
        }
    }
}
