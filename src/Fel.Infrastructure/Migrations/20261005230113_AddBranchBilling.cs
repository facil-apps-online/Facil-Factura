using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBranchBilling : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ClientIntegratorBillings_ClientId_IntegratorId",
                table: "ClientIntegratorBillings");

            migrationBuilder.AddColumn<Guid>(
                name: "BranchId",
                table: "ClientIntegratorBillings",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeactivatedAt",
                table: "Branches",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PricePerDocument",
                table: "Branches",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "SubscriptionRate",
                table: "Branches",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            // La tarifa del Client pasa a su sucursal principal (idempotente: solo si la sucursal sigue sin tarifa). Las columnas de Client
            // se conservan hasta la limpieza final.
            migrationBuilder.Sql(@"
                UPDATE b
                SET b.SubscriptionRate = c.SubscriptionRate, b.PricePerDocument = c.PricePerDocument
                FROM Branches b
                INNER JOIN Clients c ON c.Id = b.ClientId
                WHERE b.IsMain = 1 AND b.SubscriptionRate = 0 AND b.PricePerDocument = 0;");

            // Las tarifas por integrador existentes quedan en la sucursal principal de su Client. Va antes de crear el índice único y la
            // llave foránea: hasta aquí todas valen Guid.Empty.
            migrationBuilder.Sql(@"
                UPDATE cib
                SET cib.BranchId = b.Id
                FROM ClientIntegratorBillings cib
                INNER JOIN Branches b ON b.ClientId = cib.ClientId AND b.IsMain = 1
                WHERE cib.BranchId = '00000000-0000-0000-0000-000000000000';");

            migrationBuilder.CreateIndex(
                name: "IX_ClientIntegratorBillings_BranchId_IntegratorId",
                table: "ClientIntegratorBillings",
                columns: new[] { "BranchId", "IntegratorId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClientIntegratorBillings_ClientId",
                table: "ClientIntegratorBillings",
                column: "ClientId");

            migrationBuilder.AddForeignKey(
                name: "FK_ClientIntegratorBillings_Branches_BranchId",
                table: "ClientIntegratorBillings",
                column: "BranchId",
                principalTable: "Branches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ClientIntegratorBillings_Branches_BranchId",
                table: "ClientIntegratorBillings");

            migrationBuilder.DropIndex(
                name: "IX_ClientIntegratorBillings_BranchId_IntegratorId",
                table: "ClientIntegratorBillings");

            migrationBuilder.DropIndex(
                name: "IX_ClientIntegratorBillings_ClientId",
                table: "ClientIntegratorBillings");

            migrationBuilder.DropColumn(
                name: "BranchId",
                table: "ClientIntegratorBillings");

            migrationBuilder.DropColumn(
                name: "DeactivatedAt",
                table: "Branches");

            migrationBuilder.DropColumn(
                name: "PricePerDocument",
                table: "Branches");

            migrationBuilder.DropColumn(
                name: "SubscriptionRate",
                table: "Branches");

            migrationBuilder.CreateIndex(
                name: "IX_ClientIntegratorBillings_ClientId_IntegratorId",
                table: "ClientIntegratorBillings",
                columns: new[] { "ClientId", "IntegratorId" },
                unique: true);
        }
    }
}
