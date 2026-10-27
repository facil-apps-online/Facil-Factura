using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Fel.Core.Entities;
using Fel.Core.Interfaces;
using Fel.Infrastructure.Data;
using Fel.Infrastructure.Dataico;
using Microsoft.EntityFrameworkCore;

namespace Fel.Infrastructure.Services
{
    // Numeraciones de notas crédito y débito de un Client. Un Client puede tener varias numeraciones propias por tipo, cada una con su prefijo y su
    // consecutivo, y una predeterminada: la que usan las notas que no eligen otra. Sin ninguna propia, las notas siguen usando el contador de su
    // resolución de factura (su prefijo). En Dataico la numeración debe existir en la cuenta con ese prefijo: su API no permite crearla, solo listarla.
    public sealed class NoteNumberingAdminService
    {
        private static readonly NoteKind[] OwnKinds = { NoteKind.CreditNote, NoteKind.DebitNote };

        private readonly FelDbContext _db;
        private readonly IDataicoApiService _dataico;
        private readonly ICryptoService _crypto;

        public NoteNumberingAdminService(FelDbContext db, IDataicoApiService dataico, ICryptoService crypto)
        {
            _db = db;
            _dataico = dataico;
            _crypto = crypto;
        }

        public sealed record Item(Guid? Id, NoteKind Kind, string Prefix, long NextNumber, string Source, string? BranchName, bool IsDefault, bool InUse);

        public sealed record Overview(bool UsesDataico, List<Item> Items);

        // Resultado de buscar el prefijo en la cuenta de Dataico. Checked = false: no se pudo consultar (o el Client no usa Dataico).
        public sealed record DataicoCheck(bool Checked, bool Exists, string? Message);

        public sealed record SaveResult(NoteNumbering? Row, string? Error, DataicoCheck? Dataico);

        public static string KindLabel(NoteKind kind) => NoteKinds.All.First(k => k.Kind == kind).Label;

        private static string? ModuleOf(NoteKind kind) => kind switch
        {
            NoteKind.CreditNote => "credit-note",
            NoteKind.DebitNote => "debit-note",
            _ => null
        };

        public async Task<bool> UsesDataicoAsync(Guid clientId) =>
            await _db.Clients.AsNoTracking().Where(c => c.Id == clientId).Select(c => c.Integrator.Code).FirstOrDefaultAsync() == "DATAICO";

        public async Task<Overview> GetOverviewAsync(Guid clientId)
        {
            var rows = await _db.NoteNumberings.AsNoTracking()
                .Where(n => n.ClientId == clientId && n.Kind != NoteKind.SupportAdjustment)
                .Select(n => new { n.Id, n.Kind, n.Prefix, n.NextNumber, n.IsDefault, n.BranchId, BranchName = n.Branch != null ? n.Branch.Name : null, n.ResolutionId })
                .ToListAsync();

            var resolutionPrefixes = await _db.Resolutions.AsNoTracking()
                .Where(r => r.ClientId == clientId && r.DocumentType == "FE")
                .Select(r => new { r.Id, r.Prefix }).ToDictionaryAsync(r => r.Id, r => r.Prefix);

            // Notas ya emitidas por (tipo, prefijo): una numeración con notas emitidas no se borra ni cambia de prefijo.
            var used = (await _db.Documents.AsNoTracking()
                    .Where(d => d.ClientId == clientId && (d.TypeCode == "NC" || d.TypeCode == "ND") && d.Prefix != null && d.Prefix != "")
                    .Select(d => new { d.TypeCode, d.Prefix }).Distinct().ToListAsync())
                .Select(d => (Kind: d.TypeCode == "NC" ? NoteKind.CreditNote : NoteKind.DebitNote, Prefix: d.Prefix!.ToUpperInvariant()))
                .ToHashSet();

            var items = new List<Item>();

            // Propias del Client.
            foreach (var r in rows.Where(r => r.BranchId == null && r.ResolutionId == null && !string.IsNullOrWhiteSpace(r.Prefix)))
                items.Add(new Item(r.Id, r.Kind, r.Prefix!, r.NextNumber ?? 1, "own", null, r.IsDefault, used.Contains((r.Kind, r.Prefix!.ToUpperInvariant()))));

            // Las de sucursal con numeración propia.
            foreach (var r in rows.Where(r => r.BranchId != null && !string.IsNullOrWhiteSpace(r.Prefix)))
                items.Add(new Item(null, r.Kind, r.Prefix!, r.NextNumber ?? 1, "branch", r.BranchName, false, used.Contains((r.Kind, r.Prefix!.ToUpperInvariant()))));

            // Las de la resolución de factura: el prefijo es el de la factura; las resoluciones con el mismo prefijo comparten un contador.
            var byResolution = rows
                .Where(r => r.BranchId == null && r.ResolutionId != null && resolutionPrefixes.ContainsKey(r.ResolutionId.Value))
                .GroupBy(r => (r.Kind, Prefix: resolutionPrefixes[r.ResolutionId!.Value]));
            foreach (var g in byResolution)
                items.Add(new Item(null, g.Key.Kind, g.Key.Prefix, g.Max(r => r.NextNumber) ?? 1, "invoice", null, false, used.Contains((g.Key.Kind, g.Key.Prefix.ToUpperInvariant()))));

            return new Overview(await UsesDataicoAsync(clientId), items.OrderBy(i => i.Kind).ThenBy(i => i.Prefix).ToList());
        }

        // Prefijos que ya existen en la cuenta de Dataico del Client y todavía no son numeraciones propias suyas (para traerlos en lugar de digitarlos).
        public async Task<(List<(NoteKind Kind, string Prefix)> Missing, string? Error)> DataicoPrefixesNotInFacilFacturaAsync(Guid clientId)
        {
            var client = await _db.Clients.Include(c => c.Integrator).FirstOrDefaultAsync(c => c.Id == clientId);
            if (client == null || client.Integrator?.Code != "DATAICO") return (new(), "Este cliente no factura por Dataico.");
            var credentials = DataicoDocumentMapper.ToCredentials(client, _crypto);
            if (string.IsNullOrWhiteSpace(credentials.AuthToken)) return (new(), "Este cliente no tiene configurado el token de Dataico.");

            var taken = (await _db.NoteNumberings.AsNoTracking().Where(n => n.ClientId == clientId && n.Prefix != null).Select(n => new { n.Kind, n.Prefix }).ToListAsync())
                .Select(n => (n.Kind, Prefix: n.Prefix!.ToUpperInvariant())).ToHashSet();
            var invoicePrefixes = (await _db.Resolutions.AsNoTracking().Where(r => r.ClientId == clientId && r.DocumentType == "FE").Select(r => r.Prefix).ToListAsync())
                .Select(p => p.ToUpperInvariant()).ToHashSet();

            var missing = new List<(NoteKind, string)>();
            foreach (var kind in OwnKinds)
            {
                var listed = await _dataico.ListNotePrefixesAsync(ModuleOf(kind)!, credentials);
                if (!listed.Success) return (new(), $"{KindLabel(kind)}: {listed.Error}");
                foreach (var prefix in listed.Prefixes.Select(p => ResolutionPrefix.Normalize(p)).Where(p => p.Length > 0).Distinct())
                    if (!taken.Contains((kind, prefix)) && !invoicePrefixes.Contains(prefix)) missing.Add((kind, prefix));
            }
            return (missing, null);
        }

        // ¿Existe el prefijo en la numeración de notas de la cuenta de Dataico del Client? Nunca lanza: la numeración ya quedó guardada.
        public async Task<DataicoCheck> CheckDataicoAsync(Guid clientId, NoteKind kind, string prefix)
        {
            try
            {
                var module = ModuleOf(kind);
                if (module == null) return new DataicoCheck(false, false, null);
                var client = await _db.Clients.Include(c => c.Integrator).FirstOrDefaultAsync(c => c.Id == clientId);
                if (client == null || client.Integrator?.Code != "DATAICO") return new DataicoCheck(false, false, null);
                var credentials = DataicoDocumentMapper.ToCredentials(client, _crypto);
                if (string.IsNullOrWhiteSpace(credentials.AuthToken)) return new DataicoCheck(false, false, "Este cliente no tiene configurado el token de Dataico.");

                var listed = await _dataico.ListNotePrefixesAsync(module, credentials);
                if (!listed.Success) return new DataicoCheck(false, false, $"No se pudo consultar la cuenta de Dataico: {listed.Error}");
                if (listed.Prefixes.Any(p => string.Equals(ResolutionPrefix.Normalize(p), prefix, StringComparison.OrdinalIgnoreCase)))
                    return new DataicoCheck(true, true, "Ya existe en Dataico.");

                var where = kind == NoteKind.CreditNote ? "Ventas, Notas crédito, Numeraciones" : "Ventas, Notas débito, Numeraciones";
                return new DataicoCheck(true, false, $"Dataico no tiene una numeración de {(kind == NoteKind.CreditNote ? "notas crédito" : "notas débito")} con el prefijo {prefix}. Créala en Dataico ({where}) antes de emitir: su API no permite crearla desde aquí.");
            }
            catch (Exception ex)
            {
                return new DataicoCheck(false, false, $"No se pudo consultar la cuenta de Dataico: {ex.Message}");
            }
        }

        // Prefijo válido: mayúsculas, letras y números, hasta 10. Devuelve el prefijo normalizado o el error.
        private static (string Prefix, string? Error) ValidatePrefix(string? raw)
        {
            var prefix = ResolutionPrefix.Normalize(raw);
            if (prefix.Length == 0) return (prefix, "El prefijo es obligatorio.");
            if (prefix.Length > 10) return (prefix, "El prefijo no puede tener más de 10 caracteres.");
            if (!prefix.All(char.IsLetterOrDigit)) return (prefix, "El prefijo solo puede tener letras y números.");
            return (prefix, null);
        }

        // El prefijo es único por Client y tipo de nota: lo identifican la DIAN y Dataico. Tampoco puede ser el de una resolución de factura, porque
        // las notas que usan ese prefijo ya tienen su contador y repetirían números.
        private async Task<string?> PrefixConflictAsync(Guid clientId, NoteKind kind, string prefix, Guid? ignoreId)
        {
            if (await _db.NoteNumberings.AnyAsync(n => n.ClientId == clientId && n.Kind == kind && n.Prefix == prefix && (ignoreId == null || n.Id != ignoreId)))
                return "Ese prefijo ya lo usa otra numeración de este tipo de nota.";
            if (await _db.Resolutions.AnyAsync(r => r.ClientId == clientId && r.DocumentType == "FE" && r.Prefix == prefix))
                return "Ese prefijo es el de una resolución de factura: las notas con el prefijo de la factura ya tienen su propio consecutivo.";
            return null;
        }

        public async Task<SaveResult> CreateAsync(Guid clientId, NoteKind kind, string? rawPrefix, long? nextNumber)
        {
            if (!OwnKinds.Contains(kind)) return new SaveResult(null, "Solo las notas crédito y débito tienen numeraciones propias.", null);
            var (prefix, error) = ValidatePrefix(rawPrefix);
            if (error != null) return new SaveResult(null, error, null);
            if (nextNumber is < 1) return new SaveResult(null, "El consecutivo debe ser mayor a 0.", null);
            var conflict = await PrefixConflictAsync(clientId, kind, prefix, null);
            if (conflict != null) return new SaveResult(null, conflict, null);

            // La primera numeración propia de un tipo queda como predeterminada: es lo que el Client quiere al crearla.
            var isFirst = !await _db.NoteNumberings.AnyAsync(n => n.ClientId == clientId && n.Kind == kind && n.BranchId == null && n.ResolutionId == null && n.Prefix != null);
            var row = new NoteNumbering
            {
                Id = Guid.NewGuid(), ClientId = clientId, Kind = kind, Prefix = prefix, NextNumber = nextNumber, IsDefault = isFirst
            };
            _db.NoteNumberings.Add(row);
            await _db.SaveChangesAsync();
            return new SaveResult(row, null, await CheckDataicoAsync(clientId, kind, prefix));
        }

        private Task<NoteNumbering?> OwnRowAsync(Guid clientId, Guid id) =>
            _db.NoteNumberings.FirstOrDefaultAsync(n => n.Id == id && n.ClientId == clientId && n.BranchId == null && n.ResolutionId == null && n.Prefix != null && n.Kind != NoteKind.SupportAdjustment);

        private async Task<bool> InUseAsync(Guid clientId, NoteNumbering row)
        {
            var typeCode = row.Kind == NoteKind.CreditNote ? "NC" : "ND";
            return await _db.Documents.AnyAsync(d => d.ClientId == clientId && (d.NoteNumberingId == row.Id || (d.TypeCode == typeCode && d.Prefix == row.Prefix && d.Number != null && d.Number != "")));
        }

        // Cambia el consecutivo y, mientras no se haya emitido ninguna nota con ella, el prefijo.
        public async Task<SaveResult> UpdateAsync(Guid clientId, Guid id, string? rawPrefix, long? nextNumber)
        {
            var row = await OwnRowAsync(clientId, id);
            if (row == null) return new SaveResult(null, "La numeración no existe.", null);
            if (nextNumber is < 1) return new SaveResult(null, "El consecutivo debe ser mayor a 0.", null);

            var (prefix, error) = ValidatePrefix(rawPrefix);
            if (error != null) return new SaveResult(null, error, null);
            var prefixChanged = !string.Equals(prefix, row.Prefix, StringComparison.OrdinalIgnoreCase);
            if (prefixChanged)
            {
                if (await InUseAsync(clientId, row)) return new SaveResult(null, "Ya hay notas emitidas con este prefijo: no se puede cambiar. Crea una numeración nueva con el otro prefijo.", null);
                var conflict = await PrefixConflictAsync(clientId, row.Kind, prefix, row.Id);
                if (conflict != null) return new SaveResult(null, conflict, null);
                row.Prefix = prefix;
            }
            if (nextNumber.HasValue) row.NextNumber = nextNumber.Value;
            await _db.SaveChangesAsync();
            return new SaveResult(row, null, prefixChanged ? await CheckDataicoAsync(clientId, row.Kind, prefix) : null);
        }

        public async Task<string?> DeleteAsync(Guid clientId, Guid id)
        {
            var row = await OwnRowAsync(clientId, id);
            if (row == null) return "La numeración no existe.";
            if (await InUseAsync(clientId, row)) return "Ya hay notas emitidas con esta numeración: no se puede eliminar.";
            _db.NoteNumberings.Remove(row);
            await _db.SaveChangesAsync();
            return null;
        }

        public async Task<string?> SetDefaultAsync(Guid clientId, Guid id)
        {
            var row = await OwnRowAsync(clientId, id);
            if (row == null) return "La numeración no existe.";
            var sameKind = await _db.NoteNumberings.Where(n => n.ClientId == clientId && n.Kind == row.Kind && n.BranchId == null && n.ResolutionId == null && n.Prefix != null).ToListAsync();
            foreach (var other in sameKind) other.IsDefault = other.Id == id;
            await _db.SaveChangesAsync();
            return null;
        }

        // Sin predeterminada propia las notas usan el prefijo de su factura.
        public async Task ClearDefaultAsync(Guid clientId, NoteKind kind)
        {
            var defaults = await _db.NoteNumberings.Where(n => n.ClientId == clientId && n.Kind == kind && n.IsDefault).ToListAsync();
            foreach (var row in defaults) row.IsDefault = false;
            await _db.SaveChangesAsync();
        }

        // Numeración elegida para una nota (borrador): debe ser propia del Client y del tipo de la nota; si no, se ignora y la nota usa la predeterminada.
        public static async Task<Guid?> ValidChoiceAsync(FelDbContext db, Guid clientId, string? typeCode, Guid? noteNumberingId)
        {
            if (noteNumberingId == null || typeCode is not ("NC" or "ND")) return null;
            var kind = typeCode == "NC" ? NoteKind.CreditNote : NoteKind.DebitNote;
            var ok = await db.NoteNumberings.AsNoTracking().AnyAsync(n => n.Id == noteNumberingId && n.ClientId == clientId && n.Kind == kind
                && n.BranchId == null && n.ResolutionId == null && n.Prefix != null);
            return ok ? noteNumberingId : null;
        }
    }
}
