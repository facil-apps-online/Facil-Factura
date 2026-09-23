using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAssociates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AssociateId",
                table: "Clients",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Associates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Phone = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Associates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Associates_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Clients_AssociateId",
                table: "Clients",
                column: "AssociateId");

            migrationBuilder.CreateIndex(
                name: "IX_Associates_TenantId",
                table: "Associates",
                column: "TenantId");

            migrationBuilder.AddForeignKey(
                name: "FK_Clients_Associates_AssociateId",
                table: "Clients",
                column: "AssociateId",
                principalTable: "Associates",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Clients_Associates_AssociateId",
                table: "Clients");

            migrationBuilder.DropTable(
                name: "Associates");

            migrationBuilder.DropIndex(
                name: "IX_Clients_AssociateId",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "AssociateId",
                table: "Clients");
        }
    }
}
