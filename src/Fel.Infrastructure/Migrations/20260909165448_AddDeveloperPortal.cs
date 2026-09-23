using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDeveloperPortal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DeveloperUsers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ClientId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeveloperUsers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DeveloperUsers_Clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DeveloperUsers_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DeveloperUsers_ClientId",
                table: "DeveloperUsers",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_DeveloperUsers_Email",
                table: "DeveloperUsers",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DeveloperUsers_TenantId",
                table: "DeveloperUsers",
                column: "TenantId");

            // Tenant "Sandbox" compartido: hogar de los Clients de prueba que se auto-provisionan
            // a los developers que se registran de forma independiente (sin invitación de ningún
            // Tenant real) — ver DeveloperAuthController.Register. GUID fijo para poder
            // referenciarlo desde código sin tener que consultarlo primero.
            migrationBuilder.Sql(@"
                INSERT INTO [Tenants] (
                    [Id], [Name], [CommercialName], [Email], [TaxId], [VerificationDigit], [Address], [City],
                    [Phone], [TaxRegime], [EconomicActivity], [Slug], [LogoLightUrl], [LogoDarkUrl],
                    [PrimaryColorLight], [PrimaryColorDark], [DefaultLanguageCode], [DefaultTimezone],
                    [CreatedAt], [IsActive], [BillingMode], [ShowUsageToClients]
                ) VALUES (
                    '00000000-0000-0000-0000-000000000900', 'FacilFactura Sandbox', 'Sandbox Developers', 'sandbox@facil-factura.pro',
                    '', '', '', '', '', '', '', 'sandbox-developers', '', '', '#0f172a', '#f8fafc', 'es-CO',
                    'America/Bogota', GETUTCDATE(), 1, 0, 0
                );
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM [Tenants] WHERE [Id] = '00000000-0000-0000-0000-000000000900';");

            migrationBuilder.DropTable(
                name: "DeveloperUsers");
        }
    }
}
