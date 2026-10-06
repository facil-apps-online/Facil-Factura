using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Fel.Core.Entities;
using Fel.Core.Interfaces;
using Fel.Infrastructure.Data;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MimeKit;
using Fel.Infrastructure.Services;

namespace Fel.Infrastructure.Dian
{
    // Revisa el correo de facturación electrónica de cada Client que lo tenga configurado, baja los
    // adjuntos ZIP/XML de facturas de proveedores, y los procesa igual que la carga manual (mismo
    // parser, mismo disparo de eventos habilitados). Un buzón roto (credenciales vencidas, host
    // caído) no debe frenar el resto — se registra y se sigue con el próximo Client.
    public class ReceptionEmailPollerService
    {
        private readonly FelDbContext _dbContext;
        private readonly BranchCredentialResolver _credentialResolver;
        private readonly ICryptoService _cryptoService;
        private readonly IReceptionEventService _eventService;
        private readonly ILogger<ReceptionEmailPollerService> _logger;

        public ReceptionEmailPollerService(
            FelDbContext dbContext, BranchCredentialResolver credentialResolver, ICryptoService cryptoService, IReceptionEventService eventService,
            ILogger<ReceptionEmailPollerService> logger)
        {
            _dbContext = dbContext;
            _credentialResolver = credentialResolver;
            _cryptoService = cryptoService;
            _eventService = eventService;
            _logger = logger;
        }

        public async Task PollAllClientsAsync(CancellationToken ct)
        {
            // El buzón del Client y el de cada sucursal que tenga el suyo; un buzón roto no frena a los demás.
            var mailboxes = await _credentialResolver.ActiveReceptionMailboxesAsync(ct);
            var clients = await _dbContext.Clients.Where(c => mailboxes.Select(m => m.ClientId).Contains(c.Id)).ToDictionaryAsync(c => c.Id, ct);

            foreach (var mailbox in mailboxes)
            {
                try
                {
                    await PollMailboxAsync(mailbox, clients[mailbox.ClientId], ct);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error revisando el correo de recepción del Client {ClientId}, sucursal {BranchId} ({Email})", mailbox.ClientId, mailbox.BranchId, mailbox.User);
                }
            }
        }

        private async Task PollMailboxAsync(ReceptionMailbox mailbox, Client client, CancellationToken ct)
        {
            using var imap = new ImapClient();
            await imap.ConnectAsync(mailbox.Host, mailbox.Port, mailbox.UseSsl, ct);

            var password = _cryptoService.Decrypt(mailbox.PasswordEncrypted);
            await imap.AuthenticateAsync(mailbox.User, password, ct);

            var inbox = imap.Inbox;
            await inbox.OpenAsync(FolderAccess.ReadWrite, ct);

            var unseenIds = await inbox.SearchAsync(SearchQuery.NotSeen, ct);

            foreach (var uid in unseenIds)
            {
                var message = await inbox.GetMessageAsync(uid, ct);
                try
                {
                    await ProcessMessageAsync(message, client, mailbox.BranchId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error procesando el correo '{Subject}' del Client {ClientId}", message.Subject, client.Id);
                }
                finally
                {
                    // Se marca leído siempre, aunque haya fallado, para no reprocesar en cada
                    // ciclo un correo que ya sabemos que da error (ej. un adjunto corrupto).
                    await inbox.AddFlagsAsync(uid, MessageFlags.Seen, true, ct);
                }
            }

            await imap.DisconnectAsync(true, ct);
        }

        private async Task ProcessMessageAsync(MimeMessage message, Client client, Guid branchId)
        {
            foreach (var attachment in message.Attachments)
            {
                var fileName = attachment.ContentDisposition?.FileName ?? attachment.ContentType.Name ?? string.Empty;
                // Solo .zip: así es como de verdad llega una factura electrónica al correo
                // registrado ante la DIAN — un .xml suelto en un correo cualquiera no es una señal
                // confiable de que sea un documento electrónico real, y aceptar cualquier adjunto
                // .xml abriría la puerta a procesar cosas que no son facturas.
                if (!fileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                    continue;

                using var content = new MemoryStream();
                if (attachment is MimePart part)
                {
                    await part.Content.DecodeToAsync(content);
                }
                else
                {
                    continue;
                }
                content.Position = 0;

                string xml;
                using (var zip = new ZipArchive(content, ZipArchiveMode.Read))
                {
                    var xmlEntry = zip.Entries.FirstOrDefault(e => e.Name.EndsWith(".xml", StringComparison.OrdinalIgnoreCase));
                    if (xmlEntry == null) continue;
                    using var entryStream = xmlEntry.Open();
                    using var reader = new StreamReader(entryStream, System.Text.Encoding.UTF8);
                    xml = await reader.ReadToEndAsync();
                }

                ParsedReceivedDocument parsed;
                try
                {
                    parsed = ReceivedDocumentParser.Parse(xml);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Adjunto '{FileName}' del correo '{Subject}' no pudo interpretarse como documento electrónico — se ignora.", fileName, message.Subject);
                    continue;
                }

                var alreadyExists = await _dbContext.ReceivedDocuments.AnyAsync(d => d.ClientId == client.Id && d.Cufe == parsed.Cufe);
                if (alreadyExists) continue;

                var received = new ReceivedDocument
                {
                    Id = Guid.NewGuid(),
                    ClientId = client.Id,
                    BranchId = branchId,
                    SourceType = "Email",
                    RawXml = xml,
                    Cufe = parsed.Cufe,
                    DocumentTypeCode = parsed.DocumentTypeCode,
                    IssuerTaxId = parsed.IssuerTaxId,
                    IssuerName = parsed.IssuerName,
                    DocumentId = parsed.DocumentId,
                    IssueDate = parsed.IssueDate,
                    TotalAmount = parsed.TotalAmount,
                    ReceivedAt = DateTime.UtcNow
                };

                _dbContext.ReceivedDocuments.Add(received);
                await _dbContext.SaveChangesAsync();

                await _eventService.TriggerEnabledEventsAsync(received, client);
            }
        }
    }
}
