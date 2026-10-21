using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Fel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBulkImportSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "TaxesPending",
                table: "Products",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.InsertData(
                table: "IdentificationTypes",
                columns: new[] { "Id", "Code", "DataicoCode", "DataicoPayrollCode", "IsActive", "Name" },
                values: new object[,]
                {
                    { new Guid("30000000-0000-0000-0000-00000000000b"), "47", "PEP", null, true, "Permiso Especial de Permanencia (PEP)" },
                    { new Guid("30000000-0000-0000-0000-00000000000c"), "48", "PPT", null, true, "Permiso por Protección Temporal (PPT)" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "IdentificationTypes",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-00000000000b"));

            migrationBuilder.DeleteData(
                table: "IdentificationTypes",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-00000000000c"));

            migrationBuilder.DropColumn(
                name: "TaxesPending",
                table: "Products");
        }
    }
}
