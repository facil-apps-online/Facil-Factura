using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBranches : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "BranchId",
                table: "ReceivedDocuments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BranchId",
                table: "Documents",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "AllBranches",
                table: "ClientUsers",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "Role",
                table: "ClientUsers",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Administrador");

            migrationBuilder.CreateTable(
                name: "Branches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    IsMain = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Branches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Branches_Clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ClientUserBranches",
                columns: table => new
                {
                    ClientUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClientUserBranches", x => new { x.ClientUserId, x.BranchId });
                    table.ForeignKey(
                        name: "FK_ClientUserBranches_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClientUserBranches_ClientUsers_ClientUserId",
                        column: x => x.ClientUserId,
                        principalTable: "ClientUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ResolutionBranches",
                columns: table => new
                {
                    ResolutionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResolutionBranches", x => new { x.ResolutionId, x.BranchId });
                    table.ForeignKey(
                        name: "FK_ResolutionBranches_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ResolutionBranches_Resolutions_ResolutionId",
                        column: x => x.ResolutionId,
                        principalTable: "Resolutions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReceivedDocuments_BranchId",
                table: "ReceivedDocuments",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_BranchId",
                table: "Documents",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_Branches_ClientId_Code",
                table: "Branches",
                columns: new[] { "ClientId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Branches_ClientId_Main",
                table: "Branches",
                column: "ClientId",
                unique: true,
                filter: "[IsMain] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_ClientUserBranches_BranchId",
                table: "ClientUserBranches",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_ResolutionBranches_BranchId",
                table: "ResolutionBranches",
                column: "BranchId");

            migrationBuilder.AddForeignKey(
                name: "FK_Documents_Branches_BranchId",
                table: "Documents",
                column: "BranchId",
                principalTable: "Branches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ReceivedDocuments_Branches_BranchId",
                table: "ReceivedDocuments",
                column: "BranchId",
                principalTable: "Branches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // Relleno idempotente (se puede re-ejecutar sin duplicar): cada Client recibe su sucursal principal, que hereda
            // su estado y su fecha de creación para que el cobro por sucursal activa dé lo mismo que hoy por cliente activo.
            migrationBuilder.Sql(@"
                INSERT INTO Branches (Id, ClientId, Name, Code, IsMain, IsActive, CreatedAt)
                SELECT NEWID(), c.Id, 'Principal', 'PRINCIPAL', 1, c.IsActive, c.CreatedAt
                FROM Clients c
                WHERE NOT EXISTS (SELECT 1 FROM Branches b WHERE b.ClientId = c.Id AND b.IsMain = 1);");

            // Todo lo que ya existe queda asignado a la sucursal principal de su Client.
            migrationBuilder.Sql(@"
                UPDATE d
                SET d.BranchId = b.Id
                FROM Documents d
                INNER JOIN Branches b ON b.ClientId = d.ClientId AND b.IsMain = 1
                WHERE d.BranchId IS NULL;");

            migrationBuilder.Sql(@"
                UPDATE r
                SET r.BranchId = b.Id
                FROM ReceivedDocuments r
                INNER JOIN Branches b ON b.ClientId = r.ClientId AND b.IsMain = 1
                WHERE r.BranchId IS NULL;");

            migrationBuilder.Sql(@"
                INSERT INTO ResolutionBranches (ResolutionId, BranchId)
                SELECT r.Id, b.Id
                FROM Resolutions r
                INNER JOIN Branches b ON b.ClientId = r.ClientId AND b.IsMain = 1
                WHERE NOT EXISTS (SELECT 1 FROM ResolutionBranches rb WHERE rb.ResolutionId = r.Id AND rb.BranchId = b.Id);");

            migrationBuilder.Sql(@"
                INSERT INTO ClientUserBranches (ClientUserId, BranchId)
                SELECT u.Id, b.Id
                FROM ClientUsers u
                INNER JOIN Branches b ON b.ClientId = u.ClientId AND b.IsMain = 1
                WHERE NOT EXISTS (SELECT 1 FROM ClientUserBranches ub WHERE ub.ClientUserId = u.Id AND ub.BranchId = b.Id);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Documents_Branches_BranchId",
                table: "Documents");

            migrationBuilder.DropForeignKey(
                name: "FK_ReceivedDocuments_Branches_BranchId",
                table: "ReceivedDocuments");

            migrationBuilder.DropTable(
                name: "ClientUserBranches");

            migrationBuilder.DropTable(
                name: "ResolutionBranches");

            migrationBuilder.DropTable(
                name: "Branches");

            migrationBuilder.DropIndex(
                name: "IX_ReceivedDocuments_BranchId",
                table: "ReceivedDocuments");

            migrationBuilder.DropIndex(
                name: "IX_Documents_BranchId",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "BranchId",
                table: "ReceivedDocuments");

            migrationBuilder.DropColumn(
                name: "BranchId",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "AllBranches",
                table: "ClientUsers");

            migrationBuilder.DropColumn(
                name: "Role",
                table: "ClientUsers");
        }
    }
}
