using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Fel.Core.Entities;
using Fel.Core.Interfaces;
using Fel.Infrastructure.Data;
using Fel.Infrastructure.Dataico;
using Fel.Infrastructure.Dataico.Models;
using Microsoft.EntityFrameworkCore;

namespace Fel.Infrastructure.Services
{
    // Trae a FacilFactura las resoluciones vigentes que Dataico tiene vinculadas al NIT del Client. Dataico las consulta a la
    // DIAN con su propio certificado (GET /v2/numberings/{módulo}, con el token de la cuenta del Client), de modo que un Client Dataico no
    // necesita certificado propio ni subir el PDF de autorización. Lo comparten el tenant y el portal del cliente.
    public sealed class DataicoResolutionSyncService
    {
        // (módulo de Dataico, tipo de documento de la resolución). Dataico solo sincroniza facturas y documento soporte.
        private static readonly (string Module, string Label)[] Modules = { ("invoice", "Facturas"), ("support-doc", "Documento soporte") };

        private readonly FelDbContext _dbContext;
        private readonly IDataicoApiService _dataico;
        private readonly ICryptoService _cryptoService;
        private readonly ResolutionBranchService _resolutionBranches;

        public DataicoResolutionSyncService(FelDbContext dbContext, IDataicoApiService dataico, ICryptoService cryptoService, ResolutionBranchService resolutionBranches)
        {
            _dbContext = dbContext;
            _dataico = dataico;
            _cryptoService = cryptoService;
            _resolutionBranches = resolutionBranches;
        }

        public sealed class SyncReport
        {
            public int Created { get; set; }
            public int Updated { get; set; }
            public int Reactivated { get; set; }
            public int Unchanged { get; set; }
            public List<string> Notes { get; } = new();
            public List<string> Errors { get; } = new();
            // Lo que Dataico listó (tipo, prefijo, número de resolución): sirve para comprobar que una resolución recién creada ya la tiene.
            public List<(string DocumentType, string Prefix, string Number)> Listed { get; } = new();

            public bool Lists(string documentType, string prefix, string number) =>
                Listed.Any(l => l.DocumentType == documentType && string.Equals(l.Prefix, ResolutionPrefix.Normalize(prefix), StringComparison.OrdinalIgnoreCase) && l.Number == number.Trim());
        }

        // Estado de una resolución de Dataico frente a FacilFactura: loaded = ya activa y al día; outdated = activa pero con otro rango o vigencia en
        // Dataico; removed = estaba cargada y se eliminó o reemplazó; new = nunca se cargó; expired = ya venció.
        public static class States
        {
            public const string Loaded = "loaded";
            public const string Outdated = "outdated";
            public const string Removed = "removed";
            public const string New = "new";
            public const string Expired = "expired";
        }

        public sealed record AvailableResolution(
            string DocumentType, string Prefix, string Number, long Start, long End, DateTime StartDate, DateTime EndDate, string State, Guid? ResolutionId);

        public sealed record AvailableResult(List<AvailableResolution> Items, List<string> Errors, List<string> Notes);

        public async Task<bool> UsesDataicoAsync(Guid clientId) =>
            await _dbContext.Clients.AsNoTracking().Where(c => c.Id == clientId).Select(c => c.Integrator.Code).FirstOrDefaultAsync() == "DATAICO";

        // Tras crear una resolución a mano en un Client Dataico: sincroniza y comprueba que Dataico ya la tiene. Devuelve el aviso a
        // mostrar, o null si todo está bien o el Client no usa Dataico. Nunca lanza: la resolución ya quedó guardada en FacilFactura.
        public async Task<string?> CheckAfterCreateAsync(Guid clientId, Resolution created)
        {
            try
            {
                // Dataico solo sincroniza facturas y documento soporte; las notas llevan su propia numeración.
                if (created.DocumentType != "FE" && created.DocumentType != "POS" && created.DocumentType != "DS") return null;
                if (!await UsesDataicoAsync(clientId)) return null;
                var report = await SyncAsync(clientId);
                if (report.Lists(created.DocumentType, created.Prefix, created.ResolutionNumber)) return null;
                var reason = report.Errors.Count > 0 ? string.Join(" ", report.Errors) : "Dataico no la tiene vinculada a la cuenta todavía.";
                return $"La resolución quedó guardada en FacilFactura, pero Dataico no la reconoce: {reason} Mientras no la reconozca, Dataico rechazará los documentos que la usen.";
            }
            catch (Exception ex)
            {
                return $"La resolución quedó guardada en FacilFactura, pero no se pudo comprobar con Dataico: {ex.Message}";
            }
        }

        // Tipo de resolución de FacilFactura para una numeración de Dataico; null si no se maneja (p. ej. otro subtipo de factura).
        private static string? DocumentTypeOf(string module, string subtype)
        {
            if (module == "support-doc") return "DS";
            return subtype.ToUpperInvariant() switch
            {
                "ELECTRONICO" => "FE",
                "POS" => "POS",
                _ => null
            };
        }

        // Cliente con sus credenciales de Dataico, o el motivo por el que no se puede consultar.
        private async Task<(Client? Client, string? Error)> LoadClientAsync(Guid clientId)
        {
            var client = await _dbContext.Clients.Include(c => c.Integrator).FirstOrDefaultAsync(c => c.Id == clientId);
            if (client == null || client.Integrator?.Code != "DATAICO") return (null, "Este cliente no factura por Dataico.");
            if (string.IsNullOrWhiteSpace(DataicoDocumentMapper.ToCredentials(client, _cryptoService).AuthToken)) return (null, "Este cliente no tiene configurado el token de Dataico.");
            return (client, null);
        }

        // Lo que Dataico lista, ya clasificado por tipo de documento y con el prefijo normalizado. No escribe nada.
        private async Task<(List<(string DocumentType, DataicoSyncedResolution Synced)> Listed, List<string> Errors, List<string> Notes)> FetchAsync(Client client)
        {
            var credentials = DataicoDocumentMapper.ToCredentials(client, _cryptoService);
            var listed = new List<(string, DataicoSyncedResolution)>();
            var errors = new List<string>();
            var notes = new List<string>();

            foreach (var (module, label) in Modules)
            {
                var sync = await _dataico.SyncNumberingsAsync(module, credentials);
                foreach (var error in sync.Errors.Distinct())
                    errors.Add(Modules.Length > 1 ? $"{label}: {error}" : error);

                foreach (var synced in sync.Resolutions)
                {
                    var documentType = DocumentTypeOf(module, synced.Subtype);
                    if (documentType == null)
                    {
                        notes.Add($"{synced.Prefix} {synced.Number}: tipo de numeración '{synced.Subtype}' no manejado.");
                        continue;
                    }
                    if (string.IsNullOrWhiteSpace(synced.Number) || synced.Start <= 0 || synced.End < synced.Start || synced.StartDate == null || synced.EndDate == null)
                    {
                        notes.Add($"{synced.Prefix} {synced.Number}: Dataico la devolvió incompleta.");
                        continue;
                    }
                    synced.Prefix = ResolutionPrefix.Normalize(synced.Prefix);
                    listed.Add((documentType, synced));
                }
            }
            return (listed, errors, notes);
        }

        public async Task<SyncReport> SyncAsync(Guid clientId)
        {
            var report = new SyncReport();
            var (client, error) = await LoadClientAsync(clientId);
            if (client == null)
            {
                report.Errors.Add(error!);
                return report;
            }

            var mainBranchId = await BranchProvisioning.MainBranchIdAsync(_dbContext, clientId);
            var today = DateTime.Today;
            var (listed, errors, notes) = await FetchAsync(client);
            report.Errors.AddRange(errors);
            report.Notes.AddRange(notes);

            foreach (var (documentType, synced) in listed)
            {
                report.Listed.Add((documentType, synced.Prefix, synced.Number));
                if (synced.EndDate!.Value.Date < today)
                {
                    report.Notes.Add($"{synced.Prefix} {synced.Number}: vencida el {synced.EndDate.Value:dd/MM/yyyy}.");
                    continue;
                }

                await ApplyAsync(report, client, mainBranchId, documentType, synced, reactivate: false);
            }

            await _dbContext.SaveChangesAsync();
            return report;
        }

        // Las resoluciones que Dataico tiene vinculadas, cada una con su estado frente a FacilFactura, para decidir cuáles cargar o recargar. No escribe.
        public async Task<AvailableResult> ListAvailableAsync(Guid clientId)
        {
            var (client, error) = await LoadClientAsync(clientId);
            if (client == null) return new AvailableResult(new(), new() { error! }, new());

            var (listed, errors, notes) = await FetchAsync(client);
            var today = DateTime.Today;
            var existing = await _dbContext.Resolutions.AsNoTracking().Where(r => r.ClientId == clientId).ToListAsync();

            var items = new List<AvailableResolution>();
            foreach (var (documentType, synced) in listed)
            {
                var same = existing.Where(r => r.DocumentType == documentType && string.Equals(r.Prefix, synced.Prefix, StringComparison.OrdinalIgnoreCase) && r.ResolutionNumber == synced.Number).ToList();
                var active = same.FirstOrDefault(r => r.IsActive);
                string state;
                Guid? resolutionId = (active ?? same.FirstOrDefault())?.Id;
                if (synced.EndDate!.Value.Date < today) state = States.Expired;
                else if (active != null)
                    state = active.NumberStart == synced.Start && active.NumberEnd == synced.End && active.ValidFrom.Date == synced.StartDate!.Value.Date && active.ValidTo.Date == synced.EndDate.Value.Date
                        ? States.Loaded : States.Outdated;
                else state = same.Count > 0 ? States.Removed : States.New;

                items.Add(new AvailableResolution(documentType, synced.Prefix, synced.Number, synced.Start, synced.End, synced.StartDate!.Value, synced.EndDate.Value, state, resolutionId));
            }

            return new AvailableResult(items.OrderBy(i => i.DocumentType).ThenBy(i => i.Prefix).ThenBy(i => i.Number).ToList(), errors, notes);
        }

        // Carga, actualiza o recarga (reactiva) una sola resolución de Dataico. Devuelve el mensaje de lo hecho o el error.
        public async Task<(string? Message, string? Error)> LoadOneAsync(Guid clientId, string documentType, string prefix, string number)
        {
            var (client, error) = await LoadClientAsync(clientId);
            if (client == null) return (null, error);

            var normalized = ResolutionPrefix.Normalize(prefix);
            var (listed, errors, _) = await FetchAsync(client);
            var match = listed.FirstOrDefault(l => l.DocumentType == documentType && string.Equals(l.Synced.Prefix, normalized, StringComparison.OrdinalIgnoreCase) && l.Synced.Number == number.Trim());
            if (match.Synced == null)
                return (null, errors.Count > 0 ? string.Join(" ", errors) : "Dataico no tiene vinculada esa resolución a la cuenta.");
            if (match.Synced.EndDate!.Value.Date < DateTime.Today)
                return (null, $"La resolución venció el {match.Synced.EndDate.Value:dd/MM/yyyy}.");

            var report = new SyncReport();
            await ApplyAsync(report, client, await BranchProvisioning.MainBranchIdAsync(_dbContext, clientId), documentType, match.Synced, reactivate: true);
            if (report.Notes.Count > 0 && report.Created + report.Updated + report.Reactivated == 0) return (null, string.Join(" ", report.Notes));
            await _dbContext.SaveChangesAsync();

            var what = report.Created > 0 ? "cargada" : report.Reactivated > 0 ? "recargada" : report.Updated > 0 ? "actualizada" : "sin cambios";
            return ($"Resolución {normalized} {number.Trim()} {what}.", null);
        }

        private async Task ApplyAsync(SyncReport report, Client client, Guid mainBranchId, string documentType, DataicoSyncedResolution synced, bool reactivate)
        {
            var sameNumber = await _dbContext.Resolutions.Where(r => r.ClientId == client.Id && r.DocumentType == documentType && r.Prefix == synced.Prefix && r.ResolutionNumber == synced.Number).ToListAsync();
            var current = sameNumber.FirstOrDefault(r => r.IsActive);
            if (current != null)
            {
                if (current.NumberStart == synced.Start && current.NumberEnd == synced.End && current.ValidFrom.Date == synced.StartDate!.Value.Date && current.ValidTo.Date == synced.EndDate!.Value.Date)
                {
                    report.Unchanged++;
                    return;
                }
                var next = current.NextNumber ?? current.NumberStart;
                if (next < synced.Start || next > synced.End)
                {
                    report.Notes.Add($"{synced.Prefix} {synced.Number}: el rango de Dataico ({synced.Start}-{synced.End}) no incluye el próximo número a usar ({next}); no se modificó.");
                    return;
                }
                current.NumberStart = synced.Start;
                current.NumberEnd = synced.End;
                current.ValidFrom = synced.StartDate!.Value;
                current.ValidTo = synced.EndDate!.Value;
                report.Updated++;
                return;
            }

            if (sameNumber.Count > 0)
            {
                if (!reactivate)
                {
                    // Eliminada o reemplazada en FacilFactura: Dataico la sigue listando mientras esté vigente, pero no se reactiva sola.
                    report.Notes.Add($"{synced.Prefix} {synced.Number}: ya se había eliminado o reemplazado en FacilFactura; no se reactivó. Recárgala desde \"Resoluciones en Dataico\".");
                    return;
                }
                await ReactivateAsync(report, sameNumber, documentType, synced, mainBranchId);
                return;
            }

            // Resolución nueva: reemplaza a las activas del mismo tipo y prefijo, como al crearla a mano en el portal, y hereda sus
            // sucursales; sin ninguna anterior queda en la sucursal principal. Si reemplaza a la predeterminada, hereda ese lugar.
            var replaced = await _dbContext.Resolutions.Where(r => r.ClientId == client.Id && r.DocumentType == documentType && r.Prefix == synced.Prefix && r.IsActive).ToListAsync();
            var (branchIds, branchError) = await _resolutionBranches.ResolveForNewAsync(client.Id, null, documentType, synced.Prefix, mainBranchId);
            if (branchError != null)
            {
                report.Notes.Add($"{synced.Prefix} {synced.Number}: {branchError}");
                return;
            }

            var replacedIds = replaced.Select(r => r.Id).ToList();
            var otherActiveOfType = await _dbContext.Resolutions.AnyAsync(r => r.ClientId == client.Id && r.DocumentType == documentType && r.IsActive && !replacedIds.Contains(r.Id));
            foreach (var old in replaced) old.IsActive = false;

            var resolution = new Resolution
            {
                Id = Guid.NewGuid(),
                ClientId = client.Id,
                ResolutionNumber = synced.Number,
                Prefix = synced.Prefix,
                NumberStart = synced.Start,
                NumberEnd = synced.End,
                ValidFrom = synced.StartDate!.Value,
                ValidTo = synced.EndDate!.Value,
                // Dataico calcula el CUFE con su propia clave: la clave técnica de la DIAN no se necesita ni la devuelve.
                TechnicalKey = string.Empty,
                DocumentType = documentType,
                IsActive = true,
                IsDefault = !otherActiveOfType || replaced.Any(r => r.IsDefault)
            };
            _dbContext.Resolutions.Add(resolution);
            foreach (var branchId in branchIds)
                _dbContext.ResolutionBranches.Add(BranchProvisioning.LinkResolution(resolution.Id, branchId));
            report.Created++;
        }

        // Recarga una resolución eliminada: se reactiva la misma fila (conserva su consecutivo y sus sucursales) con el rango y la vigencia de Dataico.
        // Reemplaza a las activas del mismo tipo y prefijo y solo queda como predeterminada si no hay otra del tipo (o si reemplaza a la predeterminada).
        private async Task ReactivateAsync(SyncReport report, List<Resolution> inactive, string documentType, DataicoSyncedResolution synced, Guid mainBranchId)
        {
            // La más reciente de las eliminadas con ese número (normalmente es una sola).
            var row = inactive.OrderByDescending(r => r.ValidTo).First();
            var next = row.NextNumber ?? row.NumberStart;
            if (next < synced.Start || next > synced.End)
            {
                report.Notes.Add($"{synced.Prefix} {synced.Number}: el rango de Dataico ({synced.Start}-{synced.End}) no incluye el próximo número que tenía ({next}); no se recargó.");
                return;
            }

            var replaced = await _dbContext.Resolutions.Where(r => r.ClientId == row.ClientId && r.DocumentType == documentType && r.Prefix == synced.Prefix && r.IsActive && r.Id != row.Id).ToListAsync();
            var replacedIds = replaced.Select(r => r.Id).ToList();
            var otherActiveOfType = await _dbContext.Resolutions.AnyAsync(r => r.ClientId == row.ClientId && r.DocumentType == documentType && r.IsActive && r.Id != row.Id && !replacedIds.Contains(r.Id));
            foreach (var old in replaced) old.IsActive = false;

            row.NumberStart = synced.Start;
            row.NumberEnd = synced.End;
            row.ValidFrom = synced.StartDate!.Value;
            row.ValidTo = synced.EndDate!.Value;
            row.IsActive = true;
            row.IsDefault = !otherActiveOfType || replaced.Any(r => r.IsDefault);

            if (!await _dbContext.ResolutionBranches.AnyAsync(rb => rb.ResolutionId == row.Id))
                _dbContext.ResolutionBranches.Add(BranchProvisioning.LinkResolution(row.Id, mainBranchId));
            report.Reactivated++;
        }
    }
}
