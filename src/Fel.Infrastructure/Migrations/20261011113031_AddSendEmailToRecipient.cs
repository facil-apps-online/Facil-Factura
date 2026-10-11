using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSendEmailToRecipient : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "SendEmailToRecipient",
                table: "ClientEnabledDocumentTypes",
                type: "bit",
                nullable: false,
                defaultValue: true);

            // Facturas, notas y documentos soporte ya se enviaban por correo: se conserva. La nómina nunca
            // se enviaba, así que arranca apagada y cada cliente la activa si la quiere.
            migrationBuilder.Sql(@"
                UPDATE e SET e.SendEmailToRecipient = 0
                FROM ClientEnabledDocumentTypes e
                JOIN DocumentTypes d ON d.Id = e.DocumentTypeId
                WHERE d.Code LIKE 'NE%'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SendEmailToRecipient",
                table: "ClientEnabledDocumentTypes");
        }
    }
}
