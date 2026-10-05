using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBranchApiKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LiveApiKey",
                table: "Branches",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "LiveApiSecret",
                table: "Branches",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TestApiKey",
                table: "Branches",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TestApiSecret",
                table: "Branches",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            // Cada Client sin sucursal principal la recibe ahora (cubre lo creado entre fases), ya con las llaves del Client.
            migrationBuilder.Sql(@"
                INSERT INTO Branches (Id, ClientId, Name, Code, IsMain, IsActive, CreatedAt, LiveApiKey, LiveApiSecret, TestApiKey, TestApiSecret)
                SELECT NEWID(), c.Id, 'Principal', 'PRINCIPAL', 1, c.IsActive, c.CreatedAt, c.LiveApiKey, c.LiveApiSecret, c.TestApiKey, c.TestApiSecret
                FROM Clients c
                WHERE NOT EXISTS (SELECT 1 FROM Branches b WHERE b.ClientId = c.Id AND b.IsMain = 1);");

            // Las sucursales principales que ya existían heredan las llaves actuales del Client: los valores no cambian, así
            // que las integraciones siguen funcionando. Idempotente: solo toca las que aún no tienen llaves.
            migrationBuilder.Sql(@"
                UPDATE b
                SET b.LiveApiKey = c.LiveApiKey, b.LiveApiSecret = c.LiveApiSecret, b.TestApiKey = c.TestApiKey, b.TestApiSecret = c.TestApiSecret
                FROM Branches b
                INNER JOIN Clients c ON c.Id = b.ClientId
                WHERE b.IsMain = 1 AND b.LiveApiKey = '';");

            // Relleno idempotente de lo creado entre la fase 1 y esta: documentos, recibidos, resoluciones y usuarios.
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

            // Los índices únicos se crean después de copiar las llaves (antes todas valen '').
            migrationBuilder.CreateIndex(
                name: "IX_Branches_LiveApiKey",
                table: "Branches",
                column: "LiveApiKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Branches_TestApiKey",
                table: "Branches",
                column: "TestApiKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Branches_LiveApiKey",
                table: "Branches");

            migrationBuilder.DropIndex(
                name: "IX_Branches_TestApiKey",
                table: "Branches");

            migrationBuilder.DropColumn(
                name: "LiveApiKey",
                table: "Branches");

            migrationBuilder.DropColumn(
                name: "LiveApiSecret",
                table: "Branches");

            migrationBuilder.DropColumn(
                name: "TestApiKey",
                table: "Branches");

            migrationBuilder.DropColumn(
                name: "TestApiSecret",
                table: "Branches");
        }
    }
}
