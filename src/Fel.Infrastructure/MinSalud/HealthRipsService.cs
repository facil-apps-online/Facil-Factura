using Fel.Core.Entities;
using Fel.Infrastructure.Data;
using Fel.Infrastructure.Dian;
using Fel.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace Fel.Infrastructure.MinSalud
{
    public interface IHealthRipsService
    {
        // Guarda el RIPS (si llega uno; si no, reenvía el guardado) y lo envía al Ministerio con el AttachedDocument de la factura aprobada.
        Task<DocumentRips> SubmitAsync(Document document, Client client, string? ripsJson);
    }

    // Envío del RIPS soporte de una factura de salud al Mecanismo Único de Validación. Lo usan la emisión (RIPS en el mismo JSON) y el reenvío.
    public class HealthRipsService : IHealthRipsService
    {
        private readonly FelDbContext _db;
        private readonly IMinSaludFevRipsClient _client;

        public HealthRipsService(FelDbContext db, IMinSaludFevRipsClient client)
        {
            _db = db;
            _client = client;
        }

        public async Task<DocumentRips> SubmitAsync(Document document, Client client, string? ripsJson)
        {
            var row = await _db.DocumentRips.FirstOrDefaultAsync(r => r.DocumentId == document.Id);
            if (row == null)
            {
                row = new DocumentRips { DocumentId = document.Id, RipsJson = ripsJson ?? "{}" };
                _db.DocumentRips.Add(row);
            }
            else if (!string.IsNullOrWhiteSpace(ripsJson))
            {
                row.RipsJson = ripsJson;
            }

            // Una factura que el Ministerio ya aceptó no se vuelve a enviar: se devuelve su CUV.
            if (row.Status == DocumentRips.Accepted)
            {
                await _db.SaveChangesAsync();
                return row;
            }

            var stored = await DocumentSignedXmlStore.LoadAsync(_db, document.Id);
            if (stored?.AttachedDocument == null)
            {
                row.Status = DocumentRips.Pending;
                row.Message = "La factura todavía no tiene el AttachedDocument aprobado por la DIAN; el RIPS se envía cuando lo tenga.";
                await _db.SaveChangesAsync();
                return row;
            }

            var credentials = await new BranchCredentialResolver(_db).MinSaludAsync(client.Id, document.BranchId);
            var result = await _client.SendInvoiceAsync(row.RipsJson, stored.AttachedDocument, client, credentials);

            row.Attempts++;
            row.SentAt = DateTime.UtcNow;
            row.Message = result.Message;
            row.ResponseJson = result.ResponseJson;
            row.ProcessId = result.ProcessId ?? row.ProcessId;
            if (result.Accepted)
            {
                row.Status = DocumentRips.Accepted;
                row.Cuv = result.Cuv;
            }
            else if (!result.TransportFailure)
            {
                // Veredicto del Ministerio: el RIPS (o la factura) tiene glosas de rechazo y hay que corregirlo.
                row.Status = DocumentRips.Rejected;
                row.Cuv = null;
            }
            else
            {
                // Sin veredicto (red, credenciales, autenticación): queda pendiente para reintentar tal cual.
                row.Status = DocumentRips.Pending;
            }
            await _db.SaveChangesAsync();
            return row;
        }
    }
}
