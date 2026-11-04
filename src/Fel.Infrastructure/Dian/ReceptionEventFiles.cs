using System;
using System.Text;
using System.Threading.Tasks;
using Fel.Core.Entities;
using Fel.Core.Interfaces;
using Fel.Infrastructure.Dataico;

namespace Fel.Infrastructure.Dian
{
    // El XML de un evento RADIAN: con emisión directa es el que firmamos y guardamos; con Dataico se baja de la URL que entregó.
    // Lo comparten el portal y la API. `eventRow` debe traer ReceivedDocument.Client.Integrator cargados.
    public static class ReceptionEventFiles
    {
        public enum Outcome { Ok, NotAvailable, ProviderFailed }

        public static async Task<(Outcome Outcome, byte[]? Bytes, string FileName)> GetXmlAsync(
            ReceivedDocumentEvent eventRow, IDataicoApiService dataico, ICryptoService crypto)
        {
            var name = $"{eventRow.ReceivedDocument.DocumentId}-{eventRow.EventCode}.xml";
            if (string.Equals(eventRow.ReceivedDocument.Client.Integrator?.Code, "DATAICO", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(eventRow.ProviderXmlUrl)) return (Outcome.NotAvailable, null, name);
                var file = await dataico.DownloadFileAsync(eventRow.ProviderXmlUrl, DataicoDocumentMapper.ToCredentials(eventRow.ReceivedDocument.Client, crypto));
                return file == null ? (Outcome.ProviderFailed, null, name) : (Outcome.Ok, file.Value.Bytes, name);
            }

            return string.IsNullOrWhiteSpace(eventRow.SignedXml)
                ? (Outcome.NotAvailable, null, name)
                : (Outcome.Ok, Encoding.UTF8.GetBytes(eventRow.SignedXml), name);
        }
    }
}
