using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantUserAssignments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "TenantUsers",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.CreateTable(
                name: "TenantUserAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RevokedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantUserAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TenantUserAssignments_TenantUsers_TenantUserId",
                        column: x => x.TenantUserId,
                        principalTable: "TenantUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TenantUserAssignments_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        // Restrict, no Cascade: Tenants ya llega acá por TenantUsers.TenantId
                        // en cascada; un segundo camino de cascada directo es lo que SQL Server
                        // rechaza como "multiple cascade paths".
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TenantUsers_Email",
                table: "TenantUsers",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TenantUserAssignments_TenantId",
                table: "TenantUserAssignments",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_TenantUserAssignments_TenantUserId_TenantId",
                table: "TenantUserAssignments",
                columns: new[] { "TenantUserId", "TenantId" },
                unique: true);

            // Cada TenantUser existente pertenecía a un solo tenant (su columna TenantId). Sin
            // este seed, al pasar a resolver por asignaciones ningún usuario actual tendría
            // ninguna y quedarían todos sin poder entrar.
            migrationBuilder.Sql(@"
                INSERT INTO TenantUserAssignments (Id, TenantUserId, TenantId, IsActive, CreatedAt, RevokedAt)
                SELECT NEWID(), Id, TenantId, IsActive, CreatedAt, NULL
                FROM TenantUsers;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TenantUserAssignments");

            migrationBuilder.DropIndex(
                name: "IX_TenantUsers_Email",
                table: "TenantUsers");

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "TenantUsers",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");
        }
    }
}
