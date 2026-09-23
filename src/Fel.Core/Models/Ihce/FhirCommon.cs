using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Fel.Core.Models.Ihce
{
    // Tipos FHIR R4 genéricos reutilizados por los recursos RDA (Fel.Core.Models.Ihce.*) — un
    // subconjunto deliberadamente mínimo, no una librería FHIR completa: cubre solo los campos
    // que los perfiles RDA de la guía Vulcano (https://vulcano.ihcecol.gov.co) realmente exigen
    // para Patient/Encounter/Condition/Composition. Sin poder validar contra un servidor FHIR real
    // todavía (bloqueado por no tener credenciales de Sandbox), esto es la mejor aproximación
    // fiel a las StructureDefinition publicadas — revisar/ampliar cuando lleguen credenciales.
    public class FhirMeta
    {
        [JsonPropertyName("profile")]
        public List<string> Profile { get; set; } = new();
    }

    public class FhirCoding
    {
        [JsonPropertyName("system")]
        public string System { get; set; } = string.Empty;

        [JsonPropertyName("code")]
        public string Code { get; set; } = string.Empty;

        [JsonPropertyName("display")]
        public string? Display { get; set; }
    }

    public class FhirCodeableConcept
    {
        [JsonPropertyName("coding")]
        public List<FhirCoding> Coding { get; set; } = new();

        [JsonPropertyName("text")]
        public string? Text { get; set; }
    }

    public class FhirIdentifier
    {
        [JsonPropertyName("system")]
        public string System { get; set; } = string.Empty;

        [JsonPropertyName("value")]
        public string Value { get; set; } = string.Empty;
    }

    // "reference" sigue el formato FHIR "TipoRecurso/id" (ej. "Patient/abc-123") — no una URL
    // absoluta, salvo cuando se usa un identificador lógico dentro del mismo Bundle (urn:uuid:...).
    public class FhirReference
    {
        [JsonPropertyName("reference")]
        public string Reference { get; set; } = string.Empty;

        [JsonPropertyName("display")]
        public string? Display { get; set; }
    }

    public class FhirHumanName
    {
        [JsonPropertyName("use")]
        public string? Use { get; set; }

        [JsonPropertyName("family")]
        public string? Family { get; set; }

        [JsonPropertyName("given")]
        public List<string> Given { get; set; } = new();
    }

    public class FhirAddress
    {
        [JsonPropertyName("use")]
        public string? Use { get; set; }

        [JsonPropertyName("city")]
        public string? City { get; set; }

        [JsonPropertyName("district")]
        public string? District { get; set; } // Código DIVIPOLA de municipio

        [JsonPropertyName("state")]
        public string? State { get; set; } // Departamento

        [JsonPropertyName("country")]
        public string Country { get; set; } = "CO";
    }

    public class FhirPeriod
    {
        [JsonPropertyName("start")]
        public string Start { get; set; } = string.Empty; // ISO 8601 dateTime

        [JsonPropertyName("end")]
        public string? End { get; set; }
    }

    public class FhirBundleEntry
    {
        [JsonPropertyName("fullUrl")]
        public string FullUrl { get; set; } = string.Empty; // urn:uuid:{id}

        [JsonPropertyName("resource")]
        public object Resource { get; set; } = new();
    }

    public class FhirBundle
    {
        [JsonPropertyName("resourceType")]
        public string ResourceType { get; } = "Bundle";

        [JsonPropertyName("type")]
        public string Type { get; set; } = "document";

        [JsonPropertyName("timestamp")]
        public string Timestamp { get; set; } = string.Empty;

        [JsonPropertyName("entry")]
        public List<FhirBundleEntry> Entry { get; set; } = new();
    }
}
