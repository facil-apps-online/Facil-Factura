using Fel.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fel.Infrastructure.Migrations
{
    [DbContext(typeof(FelDbContext))]
    [Migration("20261022203000_AddReceivedDocumentCreditFlag")]
    public partial class AddReceivedDocumentCreditFlag : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(name: "IsCredit", table: "ReceivedDocuments", type: "bit", nullable: false, defaultValue: false);
            migrationBuilder.Sql(@"
                UPDATE ReceivedDocuments
                SET IsCredit = 1
                WHERE TRY_CONVERT(xml, RawXml).exist('//*[local-name()=""PaymentMeans""]/*[local-name()=""ID"" and text()=""2""]') = 1;");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "IsCredit", table: "ReceivedDocuments");
        }
    }
}
