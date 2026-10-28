using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSandboxAndBillingTerms : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "BillingStartsAt",
                table: "Tenants",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsBillingExempt",
                table: "Tenants",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "SentToSandbox",
                table: "Documents",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsDemo",
                table: "Clients",
                type: "bit",
                nullable: false,
                defaultValue: false);

            // El cliente que usa la cuenta demo de Dataico es de demostración.
            migrationBuilder.Sql("UPDATE Clients SET IsDemo = 1 WHERE DataicoApiUser = N'demo@dataico.com'");

            // Lo ya enviado al sandbox: documentos de sandbox de developers, de clientes de demostración y de clientes de Dataico en ambiente de pruebas o con la
            // cuenta demo. Se marca según el estado actual del cliente; lo que ya estaba en producción queda en producción.
            migrationBuilder.Sql(@"
                UPDATE d SET SentToSandbox = 1
                FROM Documents d
                JOIN Clients c ON c.Id = d.ClientId
                JOIN Integrators i ON i.Id = c.IntegratorId
                WHERE c.IsDeveloperSandbox = 1 OR c.IsDemo = 1
                   OR (i.Code = N'DATAICO' AND (c.DataicoEnvironment = N'PRUEBAS' OR c.DataicoApiUser = N'demo@dataico.com'))");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BillingStartsAt",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "IsBillingExempt",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "SentToSandbox",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "IsDemo",
                table: "Clients");
        }
    }
}
