using System;
using System.Linq;
using System.Threading.Tasks;
using Fel.Core.Entities;
using Fel.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Fel.Infrastructure.Services
{
    public static class ReceptionEventNumberingService
    {
        public static async Task<ReceptionEventNumbering> GetOrCreateAsync(FelDbContext db, Guid clientId)
        {
            var row = await db.ReceptionEventNumberings.FirstOrDefaultAsync(x => x.ClientId == clientId);
            if (row != null) return row;
            row = new ReceptionEventNumbering { Id = Guid.NewGuid(), ClientId = clientId };
            db.ReceptionEventNumberings.Add(row);
            await db.SaveChangesAsync();
            return row;
        }

        public static async Task<(string Prefix, long NextNumber)> GetAsync(FelDbContext db, Guid clientId)
        {
            var row = await db.ReceptionEventNumberings.AsNoTracking().FirstOrDefaultAsync(x => x.ClientId == clientId);
            return (row?.Prefix ?? "EV", row?.NextNumber ?? 1);
        }

        public static async Task<string?> UpdateAsync(FelDbContext db, Guid clientId, string prefix, long nextNumber)
        {
            prefix = prefix.Trim().ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(prefix) || prefix.Length > 10) return "El prefijo es obligatorio y debe tener máximo 10 caracteres.";
            if (nextNumber < 1) return "El próximo consecutivo debe ser mayor que cero.";
            var row = await GetOrCreateAsync(db, clientId);
            row.Prefix = prefix;
            row.NextNumber = nextNumber;
            return null;
        }

        public static async Task<long> ClaimNextAsync(FelDbContext db, Guid clientId)
        {
            while (true)
            {
                var row = await db.ReceptionEventNumberings.AsNoTracking().Where(x => x.ClientId == clientId).FirstOrDefaultAsync();
                if (row == null) { await GetOrCreateAsync(db, clientId); continue; }
                var affected = await db.ReceptionEventNumberings.Where(x => x.Id == row.Id && x.NextNumber == row.NextNumber)
                    .ExecuteUpdateAsync(s => s.SetProperty(x => x.NextNumber, x => x.NextNumber + 1));
                if (affected > 0) return row.NextNumber;
            }
        }

        public static async Task<long> PeekNextAsync(FelDbContext db, Guid clientId)
        {
            var row = await GetOrCreateAsync(db, clientId);
            return row.NextNumber;
        }

        public static Task<int> AdvanceIfCurrentAsync(FelDbContext db, Guid clientId, long number) =>
            db.ReceptionEventNumberings
                .Where(x => x.ClientId == clientId && x.NextNumber == number)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.NextNumber, x => x.NextNumber + 1L));

        public static Task<int> MoveBackIfAheadAsync(FelDbContext db, Guid clientId, long number) =>
            db.ReceptionEventNumberings
                .Where(x => x.ClientId == clientId && x.NextNumber > number)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.NextNumber, number));
    }
}
