using Fel.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fel.Infrastructure.Migrations
{
    [DbContext(typeof(FelDbContext))]
    [Migration("20261022194500_AddReceivedDocumentOriginalPdf")]
    public partial class AddReceivedDocumentOriginalPdf : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "OriginalPdf",
                table: "ReceivedDocuments",
                type: "varbinary(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OriginalPdfFileName",
                table: "ReceivedDocuments",
                type: "nvarchar(max)",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "OriginalPdf", table: "ReceivedDocuments");
            migrationBuilder.DropColumn(name: "OriginalPdfFileName", table: "ReceivedDocuments");
        }
    }
}
