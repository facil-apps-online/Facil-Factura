using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Fel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddIdentificationTypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "IdentificationTypes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IdentificationTypes", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "IdentificationTypes",
                columns: new[] { "Id", "Code", "IsActive", "Name" },
                values: new object[,]
                {
                    { new Guid("30000000-0000-0000-0000-000000000001"), "13", true, "Cédula de Ciudadanía" },
                    { new Guid("30000000-0000-0000-0000-000000000002"), "22", true, "Cédula de Extranjería" },
                    { new Guid("30000000-0000-0000-0000-000000000003"), "42", true, "Documento de Identificación Extranjero" },
                    { new Guid("30000000-0000-0000-0000-000000000004"), "31", true, "NIT" },
                    { new Guid("30000000-0000-0000-0000-000000000005"), "50", true, "NIT de Otro País" },
                    { new Guid("30000000-0000-0000-0000-000000000006"), "91", true, "NUIP" },
                    { new Guid("30000000-0000-0000-0000-000000000007"), "41", true, "Pasaporte" },
                    { new Guid("30000000-0000-0000-0000-000000000008"), "11", true, "Registro Civil" },
                    { new Guid("30000000-0000-0000-0000-000000000009"), "21", true, "Tarjeta de Extranjería" },
                    { new Guid("30000000-0000-0000-0000-00000000000a"), "12", true, "Tarjeta de Identidad" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_IdentificationTypes_Code",
                table: "IdentificationTypes",
                column: "Code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IdentificationTypes");
        }
    }
}
