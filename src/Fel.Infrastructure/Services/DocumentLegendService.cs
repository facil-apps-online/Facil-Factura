using System;
using System.Threading;
using System.Threading.Tasks;
using Fel.Core.Entities;
using Fel.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Fel.Infrastructure.Services
{
    public static class DocumentLegendType
    {
        public static string Normalize(string? documentType)
        {
            var value = (documentType ?? string.Empty).Trim().ToUpperInvariant();
            return value.StartsWith("DS", StringComparison.Ordinal) ? "DS" : "FE";
        }
    }

    public sealed class DocumentLegendService
    {
        private readonly FelDbContext _db;

        public DocumentLegendService(FelDbContext db) => _db = db;

        public async Task<string?> ResolveAsync(Client client, Resolution? resolution, string? documentType = null, CancellationToken cancellationToken = default)
        {
            var type = DocumentLegendType.Normalize(documentType ?? resolution?.DocumentType);
            var prefix = resolution?.Prefix?.Trim() ?? string.Empty;

            if (client.Id != Guid.Empty && !string.IsNullOrWhiteSpace(prefix))
            {
                var specific = await _db.DocumentLegendByPrefixes.AsNoTracking()
                    .FirstOrDefaultAsync(x => x.ClientId == client.Id && x.DocumentType == type && x.Prefix == prefix, cancellationToken);
                if (specific != null && !string.IsNullOrWhiteSpace(specific.Text))
                    return specific.Text;
            }

            var general = type == "DS" ? client.SupportDocumentLegend : client.ElectronicInvoiceLegend;
            return string.IsNullOrWhiteSpace(general) ? null : general;
        }
    }
}
