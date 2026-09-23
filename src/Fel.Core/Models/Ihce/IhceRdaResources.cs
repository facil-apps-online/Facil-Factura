using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Fel.Core.Models.Ihce
{
    // Recursos RDA (Resumen Digital de Atención en Salud) — subconjunto de los 47 perfiles FHIR
    // de la guía de implementación Vulcano (https://vulcano.ihcecol.gov.co), cubriendo por ahora
    // solo lo necesario para un documento de consulta ambulatoria (RDA-consulta.html): Patient,
    // Encounter, Condition y la Composition raíz. Las URL canónicas y cardinalidades están
    // tomadas directamente de las StructureDefinition publicadas (PatientRDA, EncounterAmbulatoryRDA,
    // ConditionRDA) — no inventadas. Sin acceso a un validador FHIR real todavía, revisar contra
    // Sandbox en cuanto haya credenciales.
    public static class IhceProfiles
    {
        private const string Base = "https://fhir.minsalud.gov.co/rda/StructureDefinition/";
        public const string Patient = Base + "PatientRDA";
        public const string EncounterAmbulatory = Base + "EncounterAmbulatoryRDA";
        public const string Condition = Base + "ConditionRDA";
    }

    public static class IhceCodeSystems
    {
        // Identificación del paciente ante el RNEC (Registraduría) — usado en Patient.identifier.
        public const string NationalPersonIdentifier = "https://fhir.minsalud.gov.co/rda/NamingSystem/RNEC";
        public const string Cie10 = "http://hl7.org/fhir/sid/icd-10";
        public const string Cups = "https://fhir.minsalud.gov.co/rda/CodeSystem/CUPS";
    }

    public class IhcePatient
    {
        [JsonPropertyName("resourceType")]
        public string ResourceType { get; } = "Patient";

        [JsonPropertyName("meta")]
        public FhirMeta Meta { get; set; } = new() { Profile = { IhceProfiles.Patient } };

        [JsonPropertyName("identifier")]
        public List<FhirIdentifier> Identifier { get; set; } = new();

        [JsonPropertyName("active")]
        public bool Active { get; set; } = true;

        [JsonPropertyName("name")]
        public List<FhirHumanName> Name { get; set; } = new();

        [JsonPropertyName("gender")]
        public string? Gender { get; set; } // male | female | other | unknown

        [JsonPropertyName("birthDate")]
        public string BirthDate { get; set; } = string.Empty; // YYYY-MM-DD

        [JsonPropertyName("address")]
        public List<FhirAddress> Address { get; set; } = new();
    }

    public class IhceEncounterDiagnosis
    {
        [JsonPropertyName("condition")]
        public FhirReference Condition { get; set; } = new();

        // Orden de la condición: 1 = diagnóstico principal, 2-4 = comorbilidades (ver
        // EncounterAmbulatoryRDA.diagnosis, sliced por rank).
        [JsonPropertyName("rank")]
        public int Rank { get; set; } = 1;
    }

    public class IhceEncounter
    {
        [JsonPropertyName("resourceType")]
        public string ResourceType { get; } = "Encounter";

        [JsonPropertyName("meta")]
        public FhirMeta Meta { get; set; } = new() { Profile = { IhceProfiles.EncounterAmbulatory } };

        [JsonPropertyName("status")]
        public string Status { get; } = "finished";

        [JsonPropertyName("class")]
        public FhirCoding Class { get; } = new() { System = "http://terminology.hl7.org/CodeSystem/v3-ActCode", Code = "AMB", Display = "ambulatory" };

        // serviceType: clasificación CUPS de la consulta (EncounterAmbulatoryRDA.serviceType, 1..1).
        [JsonPropertyName("serviceType")]
        public FhirCodeableConcept ServiceType { get; set; } = new();

        [JsonPropertyName("subject")]
        public FhirReference Subject { get; set; } = new();

        [JsonPropertyName("period")]
        public FhirPeriod Period { get; set; } = new();

        [JsonPropertyName("reasonCode")]
        public List<FhirCodeableConcept> ReasonCode { get; set; } = new();

        [JsonPropertyName("diagnosis")]
        public List<IhceEncounterDiagnosis> Diagnosis { get; set; } = new();

        [JsonPropertyName("serviceProvider")]
        public FhirReference? ServiceProvider { get; set; }
    }

    public class IhceCondition
    {
        [JsonPropertyName("resourceType")]
        public string ResourceType { get; } = "Condition";

        [JsonPropertyName("meta")]
        public FhirMeta Meta { get; set; } = new() { Profile = { IhceProfiles.Condition } };

        [JsonPropertyName("clinicalStatus")]
        public FhirCodeableConcept ClinicalStatus { get; set; } = new()
        {
            Coding = { new FhirCoding { System = "http://terminology.hl7.org/CodeSystem/condition-clinical", Code = "active" } }
        };

        [JsonPropertyName("category")]
        public List<FhirCodeableConcept> Category { get; set; } = new()
        {
            new FhirCodeableConcept
            {
                Coding = { new FhirCoding { System = "http://terminology.hl7.org/CodeSystem/condition-category", Code = "encounter-diagnosis" } }
            }
        };

        // Diagnóstico CIE-10 — mismo catálogo que ya usamos para RIPS (RipsCie10Rule).
        [JsonPropertyName("code")]
        public FhirCodeableConcept Code { get; set; } = new();

        [JsonPropertyName("subject")]
        public FhirReference Subject { get; set; } = new();
    }

    public class IhceCompositionSection
    {
        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        [JsonPropertyName("code")]
        public FhirCodeableConcept? Code { get; set; }

        [JsonPropertyName("entry")]
        public List<FhirReference> Entry { get; set; } = new();
    }

    public class IhceComposition
    {
        [JsonPropertyName("resourceType")]
        public string ResourceType { get; } = "Composition";

        [JsonPropertyName("status")]
        public string Status { get; } = "final";

        [JsonPropertyName("subject")]
        public FhirReference Subject { get; set; } = new();

        [JsonPropertyName("encounter")]
        public FhirReference Encounter { get; set; } = new();

        [JsonPropertyName("date")]
        public string Date { get; set; } = string.Empty;

        [JsonPropertyName("author")]
        public List<FhirReference> Author { get; set; } = new();

        [JsonPropertyName("title")]
        public string Title { get; set; } = "Resumen Digital de Atención en Salud - Consulta Ambulatoria";

        // Solo se incluyen las secciones con contenido real — la guía define 9 (Pagadores, Datos
        // demográficos adicionales, Incapacidad, Medicamentos, Alergias, Diagnósticos, Factores de
        // riesgo, Órdenes/solicitudes, Documentos de soporte); acá solo armamos Diagnósticos, que es
        // lo único que mapeamos desde MuvConsulta por ahora.
        [JsonPropertyName("section")]
        public List<IhceCompositionSection> Section { get; set; } = new();
    }
}
