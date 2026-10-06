using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveClientBranchColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BranchReceptionEvents",
                columns: table => new
                {
                    BranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AutoSendAcuseRecibo = table.Column<bool>(type: "bit", nullable: false),
                    AutoSendReciboBien = table.Column<bool>(type: "bit", nullable: false),
                    AutoSendAceptacion = table.Column<bool>(type: "bit", nullable: false),
                    AutoSendReclamo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BranchReceptionEvents", x => x.BranchId);
                    table.ForeignKey(
                        name: "FK_BranchReceptionEvents_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // Las credenciales, el buzón y los eventos que eran del Client pasan a su sucursal principal (el valor por defecto de las demás). Es
            // idempotente: no pisa lo que la principal ya tenga y solo copia a los Clients que tenían algo configurado.
            migrationBuilder.Sql(@"
                INSERT INTO BranchMinSaludCredentials (BranchId, Environment, UserType, IdentificationType, IdentificationNumber, PasswordEncrypted, TestIdentificationType, TestIdentificationNumber, TestPasswordEncrypted)
                SELECT b.Id, CASE WHEN c.MinSaludEnvironment IN ('Test', 'Production') THEN c.MinSaludEnvironment ELSE 'Test' END, c.MinSaludUserType, c.MinSaludIdentificationType, c.MinSaludIdentificationNumber, c.MinSaludPasswordEncrypted,
                       c.MinSaludTestIdentificationType, c.MinSaludTestIdentificationNumber, c.MinSaludTestPasswordEncrypted
                FROM Branches b JOIN Clients c ON c.Id = b.ClientId
                WHERE b.IsMain = 1 AND NOT EXISTS (SELECT 1 FROM BranchMinSaludCredentials x WHERE x.BranchId = b.Id)
                  AND (NULLIF(c.MinSaludIdentificationNumber, '') IS NOT NULL OR NULLIF(c.MinSaludPasswordEncrypted, '') IS NOT NULL
                    OR NULLIF(c.MinSaludTestIdentificationNumber, '') IS NOT NULL OR NULLIF(c.MinSaludTestPasswordEncrypted, '') IS NOT NULL OR NULLIF(c.MinSaludUserType, '') IS NOT NULL);
            ");

            migrationBuilder.Sql(@"
                INSERT INTO BranchIhceCredentials (BranchId, ClientId, ClientSecretEncrypted, ApimSubscriptionKeyEncrypted, TenantId, Endpoint, Environment)
                SELECT b.Id, c.IhceClientId, c.IhceClientSecretEncrypted, c.IhceApimSubscriptionKey, c.IhceTenantId, c.IhceEndpoint, CASE WHEN c.IhceEnvironment IN ('Sandbox', 'Production') THEN c.IhceEnvironment ELSE 'Sandbox' END
                FROM Branches b JOIN Clients c ON c.Id = b.ClientId
                WHERE b.IsMain = 1 AND NOT EXISTS (SELECT 1 FROM BranchIhceCredentials x WHERE x.BranchId = b.Id)
                  AND (NULLIF(c.IhceClientId, '') IS NOT NULL OR NULLIF(c.IhceClientSecretEncrypted, '') IS NOT NULL OR NULLIF(c.IhceApimSubscriptionKey, '') IS NOT NULL
                    OR NULLIF(c.IhceTenantId, '') IS NOT NULL OR NULLIF(c.IhceEndpoint, '') IS NOT NULL);
            ");

            migrationBuilder.Sql(@"
                INSERT INTO BranchReceptionMailboxes (BranchId, Enabled, Host, Port, UseSsl, [User], PasswordEncrypted)
                SELECT b.Id, c.ReceptionEmailEnabled, c.ReceptionEmailHost, CASE WHEN c.ReceptionEmailPort BETWEEN 1 AND 65535 THEN c.ReceptionEmailPort ELSE 993 END, c.ReceptionEmailUseSsl, c.ReceptionEmailUser, c.ReceptionEmailPasswordEncrypted
                FROM Branches b JOIN Clients c ON c.Id = b.ClientId
                WHERE b.IsMain = 1 AND NOT EXISTS (SELECT 1 FROM BranchReceptionMailboxes x WHERE x.BranchId = b.Id)
                  AND (c.ReceptionEmailEnabled = 1 OR NULLIF(c.ReceptionEmailHost, '') IS NOT NULL OR NULLIF(c.ReceptionEmailUser, '') IS NOT NULL OR NULLIF(c.ReceptionEmailPasswordEncrypted, '') IS NOT NULL);
            ");

            migrationBuilder.Sql(@"
                INSERT INTO BranchReceptionEvents (BranchId, AutoSendAcuseRecibo, AutoSendReciboBien, AutoSendAceptacion, AutoSendReclamo)
                SELECT b.Id, c.AutoSendAcuseRecibo, c.AutoSendReciboBien, c.AutoSendAceptacion, c.AutoSendReclamo
                FROM Branches b JOIN Clients c ON c.Id = b.ClientId
                WHERE b.IsMain = 1 AND NOT EXISTS (SELECT 1 FROM BranchReceptionEvents x WHERE x.BranchId = b.Id)
                  AND (c.AutoSendAcuseRecibo = 1 OR c.AutoSendReciboBien = 1 OR c.AutoSendAceptacion = 1 OR c.AutoSendReclamo = 1);
            ");

            // Contadores de notas: si la columna vieja del Client va por delante del contador compartido (un servicio anterior pudo avanzarla
            // durante un despliegue), el compartido se pone al mayor de los dos antes de borrar la columna.
            migrationBuilder.Sql(@"
                UPDATE n SET n.NextNumber = c.NextCreditNoteNumber FROM NoteNumberings n JOIN Clients c ON c.Id = n.ClientId
                WHERE n.BranchId IS NULL AND n.Kind = 1 AND c.NextCreditNoteNumber IS NOT NULL AND (n.NextNumber IS NULL OR c.NextCreditNoteNumber > n.NextNumber);
                UPDATE n SET n.NextNumber = c.NextDebitNoteNumber FROM NoteNumberings n JOIN Clients c ON c.Id = n.ClientId
                WHERE n.BranchId IS NULL AND n.Kind = 2 AND c.NextDebitNoteNumber IS NOT NULL AND (n.NextNumber IS NULL OR c.NextDebitNoteNumber > n.NextNumber);
                UPDATE n SET n.NextNumber = c.NextSupportAdjustmentNumber FROM NoteNumberings n JOIN Clients c ON c.Id = n.ClientId
                WHERE n.BranchId IS NULL AND n.Kind = 3 AND c.NextSupportAdjustmentNumber IS NOT NULL AND (n.NextNumber IS NULL OR c.NextSupportAdjustmentNumber > n.NextNumber);
                UPDATE n SET n.Prefix = c.SupportAdjustmentPrefix FROM NoteNumberings n JOIN Clients c ON c.Id = n.ClientId
                WHERE n.BranchId IS NULL AND n.Kind = 3 AND n.Prefix IS NULL AND NULLIF(c.SupportAdjustmentPrefix, '') IS NOT NULL;
            ");

            // Lo que quedó sin sucursal (documentos anteriores a las sucursales) va a la principal, antes de volver obligatorio BranchId.
            migrationBuilder.Sql(@"
                UPDATE d SET d.BranchId = b.Id FROM Documents d JOIN Branches b ON b.ClientId = d.ClientId AND b.IsMain = 1 WHERE d.BranchId IS NULL;
                UPDATE r SET r.BranchId = b.Id FROM ReceivedDocuments r JOIN Branches b ON b.ClientId = r.ClientId AND b.IsMain = 1 WHERE r.BranchId IS NULL;
            ");

            migrationBuilder.DropIndex(
                name: "IX_Clients_LiveApiKey",
                table: "Clients");

            migrationBuilder.DropIndex(
                name: "IX_Clients_TestApiKey",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "AutoSendAceptacion",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "AutoSendAcuseRecibo",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "AutoSendReciboBien",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "AutoSendReclamo",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "IhceApimSubscriptionKey",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "IhceClientId",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "IhceClientSecretEncrypted",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "IhceEndpoint",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "IhceEnvironment",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "IhceTenantId",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "LiveApiKey",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "LiveApiSecret",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "MinSaludEnvironment",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "MinSaludIdentificationNumber",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "MinSaludIdentificationType",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "MinSaludPasswordEncrypted",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "MinSaludTestIdentificationNumber",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "MinSaludTestIdentificationType",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "MinSaludTestPasswordEncrypted",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "MinSaludUserType",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "NextCreditNoteNumber",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "NextDebitNoteNumber",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "NextSupportAdjustmentNumber",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "PricePerDocument",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "ReceptionEmailEnabled",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "ReceptionEmailHost",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "ReceptionEmailPasswordEncrypted",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "ReceptionEmailPort",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "ReceptionEmailUseSsl",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "ReceptionEmailUser",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "SubscriptionRate",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "SupportAdjustmentPrefix",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "TestApiKey",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "TestApiSecret",
                table: "Clients");

            migrationBuilder.AlterColumn<Guid>(
                name: "BranchId",
                table: "ReceivedDocuments",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "BranchId",
                table: "Documents",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "BranchId",
                table: "ReceivedDocuments",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<Guid>(
                name: "BranchId",
                table: "Documents",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<bool>(
                name: "AutoSendAceptacion",
                table: "Clients",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "AutoSendAcuseRecibo",
                table: "Clients",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "AutoSendReciboBien",
                table: "Clients",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "AutoSendReclamo",
                table: "Clients",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "IhceApimSubscriptionKey",
                table: "Clients",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IhceClientId",
                table: "Clients",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IhceClientSecretEncrypted",
                table: "Clients",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IhceEndpoint",
                table: "Clients",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IhceEnvironment",
                table: "Clients",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "IhceTenantId",
                table: "Clients",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LiveApiKey",
                table: "Clients",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "LiveApiSecret",
                table: "Clients",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "MinSaludEnvironment",
                table: "Clients",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "MinSaludIdentificationNumber",
                table: "Clients",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MinSaludIdentificationType",
                table: "Clients",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MinSaludPasswordEncrypted",
                table: "Clients",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MinSaludTestIdentificationNumber",
                table: "Clients",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MinSaludTestIdentificationType",
                table: "Clients",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MinSaludTestPasswordEncrypted",
                table: "Clients",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MinSaludUserType",
                table: "Clients",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "NextCreditNoteNumber",
                table: "Clients",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "NextDebitNoteNumber",
                table: "Clients",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "NextSupportAdjustmentNumber",
                table: "Clients",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PricePerDocument",
                table: "Clients",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "ReceptionEmailEnabled",
                table: "Clients",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ReceptionEmailHost",
                table: "Clients",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ReceptionEmailPasswordEncrypted",
                table: "Clients",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "ReceptionEmailPort",
                table: "Clients",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "ReceptionEmailUseSsl",
                table: "Clients",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ReceptionEmailUser",
                table: "Clients",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "SubscriptionRate",
                table: "Clients",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "SupportAdjustmentPrefix",
                table: "Clients",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TestApiKey",
                table: "Clients",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TestApiSecret",
                table: "Clients",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            // Copia de vuelta, desde la sucursal principal, lo que antes era del Client (reversible con datos, no solo con estructura).
            migrationBuilder.Sql(@"
                UPDATE c SET c.LiveApiKey = b.LiveApiKey, c.LiveApiSecret = b.LiveApiSecret, c.TestApiKey = b.TestApiKey, c.TestApiSecret = b.TestApiSecret,
                             c.PricePerDocument = b.PricePerDocument, c.SubscriptionRate = b.SubscriptionRate
                FROM Clients c JOIN Branches b ON b.ClientId = c.Id AND b.IsMain = 1;
            ");

            migrationBuilder.Sql(@"
                UPDATE c SET c.MinSaludEnvironment = m.Environment, c.MinSaludUserType = m.UserType, c.MinSaludIdentificationType = m.IdentificationType, c.MinSaludIdentificationNumber = m.IdentificationNumber,
                             c.MinSaludPasswordEncrypted = m.PasswordEncrypted, c.MinSaludTestIdentificationType = m.TestIdentificationType, c.MinSaludTestIdentificationNumber = m.TestIdentificationNumber,
                             c.MinSaludTestPasswordEncrypted = m.TestPasswordEncrypted
                FROM Clients c JOIN Branches b ON b.ClientId = c.Id AND b.IsMain = 1 JOIN BranchMinSaludCredentials m ON m.BranchId = b.Id;
                UPDATE c SET c.IhceClientId = i.ClientId, c.IhceClientSecretEncrypted = i.ClientSecretEncrypted, c.IhceApimSubscriptionKey = i.ApimSubscriptionKeyEncrypted,
                             c.IhceTenantId = i.TenantId, c.IhceEndpoint = i.Endpoint, c.IhceEnvironment = i.Environment
                FROM Clients c JOIN Branches b ON b.ClientId = c.Id AND b.IsMain = 1 JOIN BranchIhceCredentials i ON i.BranchId = b.Id;
                UPDATE c SET c.ReceptionEmailEnabled = r.Enabled, c.ReceptionEmailHost = r.Host, c.ReceptionEmailPort = r.Port, c.ReceptionEmailUseSsl = r.UseSsl,
                             c.ReceptionEmailUser = r.[User], c.ReceptionEmailPasswordEncrypted = r.PasswordEncrypted
                FROM Clients c JOIN Branches b ON b.ClientId = c.Id AND b.IsMain = 1 JOIN BranchReceptionMailboxes r ON r.BranchId = b.Id;
                UPDATE c SET c.AutoSendAcuseRecibo = e.AutoSendAcuseRecibo, c.AutoSendReciboBien = e.AutoSendReciboBien, c.AutoSendAceptacion = e.AutoSendAceptacion, c.AutoSendReclamo = e.AutoSendReclamo
                FROM Clients c JOIN Branches b ON b.ClientId = c.Id AND b.IsMain = 1 JOIN BranchReceptionEvents e ON e.BranchId = b.Id;
                UPDATE c SET c.NextCreditNoteNumber = (SELECT n.NextNumber FROM NoteNumberings n WHERE n.ClientId = c.Id AND n.BranchId IS NULL AND n.Kind = 1),
                             c.NextDebitNoteNumber = (SELECT n.NextNumber FROM NoteNumberings n WHERE n.ClientId = c.Id AND n.BranchId IS NULL AND n.Kind = 2),
                             c.NextSupportAdjustmentNumber = (SELECT n.NextNumber FROM NoteNumberings n WHERE n.ClientId = c.Id AND n.BranchId IS NULL AND n.Kind = 3),
                             c.SupportAdjustmentPrefix = (SELECT n.Prefix FROM NoteNumberings n WHERE n.ClientId = c.Id AND n.BranchId IS NULL AND n.Kind = 3)
                FROM Clients c;
            ");

            migrationBuilder.CreateIndex(
                name: "IX_Clients_LiveApiKey",
                table: "Clients",
                column: "LiveApiKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Clients_TestApiKey",
                table: "Clients",
                column: "TestApiKey",
                unique: true);

            migrationBuilder.DropTable(
                name: "BranchReceptionEvents");
        }
    }
}
