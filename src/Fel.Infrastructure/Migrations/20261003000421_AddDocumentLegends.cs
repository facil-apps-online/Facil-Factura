using System;
using Fel.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fel.Infrastructure.Migrations
{
    // Leyendas de los documentos: las dos de todo el Client (factura y documento soporte) y las propias de cada prefijo.
    // En producción ya está aplicada (consta en __EFMigrationsHistory con el mismo identificador), así que EF no la repite.
    [DbContext(typeof(FelDbContext))]
    [Migration("20261003000421_AddDocumentLegends")]
    public partial class AddDocumentLegends : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ElectronicInvoiceLegend",
                table: "Clients",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SupportDocumentLegend",
                table: "Clients",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "DocumentLegendByPrefixes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Prefix = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Text = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentLegendByPrefixes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentLegendByPrefixes_Clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // Con SQL explícito porque esta migración no trae modelo asociado y, sin él, EF agrega al índice único un filtro
            // "IS NOT NULL" que producción no tiene (las tres columnas no admiten nulos). Así queda idéntico al de producción.
            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX [IX_DocumentLegendByPrefixes_ClientId_DocumentType_Prefix] ON [DocumentLegendByPrefixes] ([ClientId], [DocumentType], [Prefix])");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "DocumentLegendByPrefixes");

            migrationBuilder.DropColumn(name: "ElectronicInvoiceLegend", table: "Clients");

            migrationBuilder.DropColumn(name: "SupportDocumentLegend", table: "Clients");
        }
    }
}
