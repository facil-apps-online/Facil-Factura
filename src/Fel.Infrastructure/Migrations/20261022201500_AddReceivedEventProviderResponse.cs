using Fel.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fel.Infrastructure.Migrations
{
    [DbContext(typeof(FelDbContext))]
    [Migration("20261022201500_AddReceivedEventProviderResponse")]
    public partial class AddReceivedEventProviderResponse : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(name: "ProviderPdfUrl", table: "ReceivedDocumentEvents", type: "nvarchar(max)", nullable: true);
            migrationBuilder.AddColumn<string>(name: "ProviderResponse", table: "ReceivedDocumentEvents", type: "nvarchar(max)", nullable: true);
            migrationBuilder.AddColumn<string>(name: "ProviderXmlUrl", table: "ReceivedDocumentEvents", type: "nvarchar(max)", nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "ProviderPdfUrl", table: "ReceivedDocumentEvents");
            migrationBuilder.DropColumn(name: "ProviderResponse", table: "ReceivedDocumentEvents");
            migrationBuilder.DropColumn(name: "ProviderXmlUrl", table: "ReceivedDocumentEvents");
        }
    }
}
