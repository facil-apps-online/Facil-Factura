using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAttachedDocumentToSignedXml : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "ApplicationResponseGzip",
                table: "DocumentSignedXmls",
                type: "varbinary(max)",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "AttachedDocumentGzip",
                table: "DocumentSignedXmls",
                type: "varbinary(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FileSequence",
                table: "DocumentSignedXmls",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ApplicationResponseGzip",
                table: "DocumentSignedXmls");

            migrationBuilder.DropColumn(
                name: "AttachedDocumentGzip",
                table: "DocumentSignedXmls");

            migrationBuilder.DropColumn(
                name: "FileSequence",
                table: "DocumentSignedXmls");
        }
    }
}
