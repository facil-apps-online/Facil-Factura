using System;
using Fel.Core.Entities;
using Fel.Core.Models;

namespace Fel.Infrastructure.Dian
{
    public static class ReceptionEventMapper
    {
        // Literales exactos exigidos por la DIAN para cbc:Description (Anexo Técnico v1.9, numeral
        // 6.5.5) — la validación es de coincidencia textual.
        public static string DescriptionFor(string eventCode) => eventCode switch
        {
            "030" => "Acuse de recibo de Factura Electrónica de Venta",
            "031" => "Reclamo de la Factura Electrónica de Venta",
            "032" => "Recibo del bien y prestación de servicio",
            "033" => "Aceptación expresa",
            _ => throw new NotSupportedException($"Código de evento no soportado: '{eventCode}'.")
        };

        public static UblEventData BuildEventData(ReceivedDocument received, Client client, string eventCode)
        {
            return new UblEventData
            {
                // Consecutivo propio del evento — no depende de una Resolución (los eventos no
                // tienen rango de numeración autorizado como las facturas).
                DocumentNumber = Guid.NewGuid().ToString("N")[..12].ToUpperInvariant(),
                IssueDate = Fel.Core.Models.ColombiaTime.Now,
                IssueTime = Fel.Core.Models.ColombiaTime.Now,
                Environment = client.DianHabilitationStatus == "Production" ? "1" : "2",
                SoftwarePin = client.SoftwarePin,

                SenderTaxId = client.TaxId,
                SenderName = client.CompanyName,

                ReceiverTaxId = received.IssuerTaxId,
                ReceiverName = received.IssuerName,

                ResponseCode = eventCode,
                ResponseDescription = DescriptionFor(eventCode),

                ReferencedDocumentId = received.DocumentId,
                ReferencedCufe = received.Cufe,
                ReferencedDocumentTypeCode = received.DocumentTypeCode
            };
        }
    }
}
