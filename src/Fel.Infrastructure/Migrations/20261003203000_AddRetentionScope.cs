using Microsoft.EntityFrameworkCore.Migrations;

namespace Fel.Infrastructure.Migrations;

public partial class AddRetentionScope : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "Scope",
            table: "ClientEnabledRetentionConcepts",
            type: "int",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.DropIndex(
            name: "IX_ClientEnabledRetentionConcepts_ClientId_RetentionConceptId",
            table: "ClientEnabledRetentionConcepts");

        migrationBuilder.CreateIndex(
            name: "IX_ClientEnabledRetentionConcepts_ClientId_RetentionConceptId_Scope",
            table: "ClientEnabledRetentionConcepts",
            columns: new[] { "ClientId", "RetentionConceptId", "Scope" },
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_ClientEnabledRetentionConcepts_ClientId_RetentionConceptId_Scope",
            table: "ClientEnabledRetentionConcepts");

        migrationBuilder.CreateIndex(
            name: "IX_ClientEnabledRetentionConcepts_ClientId_RetentionConceptId",
            table: "ClientEnabledRetentionConcepts",
            columns: new[] { "ClientId", "RetentionConceptId" },
            unique: true);

        migrationBuilder.DropColumn(name: "Scope", table: "ClientEnabledRetentionConcepts");
    }
}
