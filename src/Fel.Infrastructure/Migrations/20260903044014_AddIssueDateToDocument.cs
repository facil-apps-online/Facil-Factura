using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddIssueDateToDocument : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "IssueDate",
                table: "Documents",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            // Los documentos existentes no tenían fecha de factura propia; se usaba CreatedAt como
            // fecha de emisión (issue_date) al enviar a Dataico, así que se respalda con ese mismo
            // valor en vez de dejarlos en 0001-01-01.
            migrationBuilder.Sql("UPDATE [Documents] SET [IssueDate] = [CreatedAt]");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IssueDate",
                table: "Documents");
        }
    }
}
