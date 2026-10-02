using System;
using System.Text.Json;

namespace Fel.Core.Models
{
    // Contrato de servicios AIU (Administración, Imprevistos, Utilidad) — tipo de operación "09" de la
    // factura de venta (Anexo Técnico v1.9, numeral 13.2.1.1). Se guarda como JSON en
    // Document.SectorExtensionData bajo la clave "aiu", así no hace falta una columna nueva:
    //   { "aiu": { "baseAmount": 1000000, "adminPercent": 10, "unforeseenPercent": 5,
    //              "profitPercent": 5, "ivaRate": 19, "contractObject": "..." } }
    // El formulario del portal genera con esto las tres líneas (códigos AIU-A / AIU-I / AIU-U).
    public class AiuContractData
    {
        public const string AdminCode = "AIU-A";
        public const string UnforeseenCode = "AIU-I";
        public const string ProfitCode = "AIU-U";

        // Texto con el que DEBE empezar el cbc:Note de la línea de Administración; después va el
        // objeto del contrato facturado (regla del Anexo: "El contribuyente debe incluir el objeto
        // del contrato facturado").
        public const string NotePrefix = "Contrato de servicios AIU por concepto de:";

        public decimal BaseAmount { get; set; }
        public decimal AdminPercent { get; set; }
        public decimal UnforeseenPercent { get; set; }
        public decimal ProfitPercent { get; set; }
        public decimal IvaRate { get; set; } = 19;
        public string ContractObject { get; set; } = string.Empty;

        public string BuildAdminNote() => $"{NotePrefix} {ContractObject.Trim()}";

        private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

        private class Envelope { public AiuContractData? Aiu { get; set; } }

        // null si el documento no trae datos AIU (o el JSON no es válido): un documento común.
        public static AiuContractData? TryRead(string? sectorExtensionData)
        {
            if (string.IsNullOrWhiteSpace(sectorExtensionData)) return null;
            try { return JsonSerializer.Deserialize<Envelope>(sectorExtensionData, Options)?.Aiu; }
            catch (JsonException) { return null; }
        }
    }
}
