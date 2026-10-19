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
            public int Unchanged { get; set; }
            public List<string> Notes { get; } = new();
            public List<string> Errors { get; } = new();
            // Lo que Dataico listó (tipo, prefijo, número de resolución): sirve para comprobar que una resolución recién creada ya la tiene.
            public List<(string DocumentType, string Prefix, string Number)> Listed { get; } = new();

            public bool Lists(string documentType, string prefix, string number) =>
                Listed.Any(l => l.DocumentType == documentType && l.Prefix == prefix.Trim() && l.Number == number.Trim());
        }

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

        public async Task<SyncReport> SyncAsync(Guid clientId)
        {
            var report = new SyncReport();
            var client = await _dbContext.Clients.Include(c => c.Integrator).FirstOrDefaultAsync(c => c.Id == clientId);
            if (client == null || client.Integrator?.Code != "DATAICO")
            {
                report.Errors.Add("Este cliente no factura por Dataico.");
                return report;
            }

            var credentials = DataicoDocumentMapper.ToCredentials(client, _cryptoService);
            if (string.IsNullOrWhiteSpace(credentials.AuthToken))
            {
                report.Errors.Add("Este cliente no tiene configurado el token de Dataico.");
                return report;
            }

            var mainBranchId = await BranchProvisioning.MainBranchIdAsync(_dbContext, clientId);
            var today = DateTime.Today;

            foreach (var (module, label) in Modules)
            {
                var sync = await _dataico.SyncNumberingsAsync(module, credentials);
                foreach (var error in sync.Errors.Distinct())
                    report.Errors.Add(Modules.Length > 1 ? $"{label}: {error}" : error);

                foreach (var synced in sync.Resolutions)
                {
                    var documentType = DocumentTypeOf(module, synced.Subtype);
                    if (documentType == null)
                    {
                        report.Notes.Add($"{synced.Prefix} {synced.Number}: tipo de numeración '{synced.Subtype}' no manejado.");
                        continue;
                    }
                    if (string.IsNullOrWhiteSpace(synced.Number) || synced.Start <= 0 || synced.End < synced.Start || synced.StartDate == null || synced.EndDate == null)
                    {
                        report.Notes.Add($"{synced.Prefix} {synced.Number}: Dataico la devolvió incompleta.");
                        continue;
                    }

                    report.Listed.Add((documentType, synced.Prefix, synced.Number));
                    if (synced.EndDate.Value.Date < today)
                    {
                        report.Notes.Add($"{synced.Prefix} {synced.Number}: vencida el {synced.EndDate.Value:dd/MM/yyyy}.");
                        continue;
                    }

                    await ApplyAsync(report, client, mainBranchId, documentType, synced);
                }
            }

            await _dbContext.SaveChangesAsync();
            return report;
        }

        private async Task ApplyAsync(SyncReport report, Client client, Guid mainBranchId, string documentType, DataicoSyncedResolution synced)
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
                // Eliminada o reemplazada en FacilFactura: Dataico la sigue listando mientras esté vigente, pero no se reactiva sola.
                report.Notes.Add($"{synced.Prefix} {synced.Number}: ya se había eliminado o reemplazado en FacilFactura; no se reactivó.");
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
    }
}
