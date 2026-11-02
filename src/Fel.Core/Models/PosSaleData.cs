using System;
using System.Text.Json;

namespace Fel.Core.Models
{
    // Datos propios del Documento Equivalente POS (Anexo Técnico de Documento Equivalente Electrónico, numerales 8.2.1.1 y 8.2.1.2): la información de
    // la caja de venta y, si el comprador tiene programa de beneficios, su código, nombre y puntos. Se guarda como JSON en
    // Document.SectorExtensionData bajo la clave "pos", igual que el contrato AIU bajo "aiu":
    //   { "pos": { "boxPlate": "CAJA-01", "boxLocation": "ALMACEN", "cashier": "Maria Perez", "boxType": "Registradora", "saleCode": "V-1001" } }
    // El SubTotal de la caja no se guarda: sale de las líneas al generar el XML.
    public class PosSaleData
    {
        public const int MaxLength = 100;

        // Placa de inventario de la caja.
        public string BoxPlate { get; set; } = string.Empty;
        public string BoxLocation { get; set; } = string.Empty;
        // Nombres y apellidos del cajero o vendedor.
        public string Cashier { get; set; } = string.Empty;
        // El Anexo lo define como texto de 1 a 100 caracteres, sin catálogo.
        public string BoxType { get; set; } = string.Empty;
        public string SaleCode { get; set; } = string.Empty;

        // Beneficios del comprador (opcional): se informa el grupo completo o no se informa.
        public string? BuyerCode { get; set; }
        public string? BuyerName { get; set; }
        public decimal? BuyerPoints { get; set; }

        public bool HasBuyerBenefits => !string.IsNullOrWhiteSpace(BuyerCode) || !string.IsNullOrWhiteSpace(BuyerName) || BuyerPoints.HasValue;

        private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

        private class Envelope { public PosSaleData? Pos { get; set; } }

        // null si el documento no trae datos de POS (o el JSON no es válido).
        public static PosSaleData? TryRead(string? sectorExtensionData)
        {
            if (string.IsNullOrWhiteSpace(sectorExtensionData)) return null;
            try { return JsonSerializer.Deserialize<Envelope>(sectorExtensionData, Options)?.Pos; }
            catch (JsonException) { return null; }
        }
    }
}
