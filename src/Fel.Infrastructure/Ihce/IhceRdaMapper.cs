using System;
using System.Collections.Generic;
using System.Linq;
using Fel.Core.Models.Ihce;

namespace Fel.Infrastructure.Ihce
{
    // Arma un Bundle RDA de consulta ambulatoria (el primero de los 4 documentos de la guía
    // Vulcano) a partir de datos genéricos de paciente/consulta/diagnóstico — deliberadamente
    // desacoplado de MuvRipsJsonModel (Fel.Api.Tenant) para no crear una referencia de proyecto
    // Infrastructure -> Api.Tenant; el caller (un futuro IhceSubmissionService) es quien traduce
    // MuvUsuario/MuvConsulta a estos parámetros. Sin credenciales de Sandbox todavía para validar
    // esto contra un servidor FHIR real — construido fielmente a las StructureDefinition
    // publicadas (ver IhceRdaResources.cs), pendiente de revisión cuando lleguen.
    public static class IhceRdaMapper
    {
        public record PatientData(
            string IdentificationType, // CC, TI, RC, CE, PA, etc. (mismo catálogo que RIPS)
            string IdentificationNumber,
            string FirstName,
            string? SecondName,
            string FirstLastName,
            string? SecondLastName,
            string BirthDate, // YYYY-MM-DD
            string Gender, // male | female | other | unknown (ya traducido desde el código RIPS)
            string CityDivipolaCode);

        public record EncounterData(
            DateTime AttentionDate,
            string CupsServiceCode,
            string CupsServiceDisplay,
            string CausaMotivoAtencionDisplay,
            string ProviderNit);

        public record DiagnosisData(
            string Cie10Code,
            string Cie10Display,
            bool IsMainDiagnosis);

        public static FhirBundle BuildAmbulatoryConsultationBundle(
            PatientData patient,
            EncounterData encounter,
            IReadOnlyList<DiagnosisData> diagnoses)
        {
            var patientId = $"urn:uuid:{Guid.NewGuid()}";
            var encounterId = $"urn:uuid:{Guid.NewGuid()}";

            var fhirPatient = new IhcePatient
            {
                Identifier = { new FhirIdentifier { System = IhceCodeSystems.NationalPersonIdentifier, Value = patient.IdentificationNumber } },
                Name = { new FhirHumanName
                {
                    Use = "official",
                    Family = string.Join(" ", new[] { patient.FirstLastName, patient.SecondLastName }.Where(s => !string.IsNullOrWhiteSpace(s))),
                    Given = new List<string>(new[] { patient.FirstName, patient.SecondName }.Where(s => !string.IsNullOrWhiteSpace(s))!)
                } },
                Gender = patient.Gender,
                BirthDate = patient.BirthDate,
                Address = { new FhirAddress { District = patient.CityDivipolaCode, Country = "CO" } }
            };

            var conditionEntries = new List<FhirBundleEntry>();
            var diagnosisRefs = new List<IhceEncounterDiagnosis>();
            var conditionRefsForSection = new List<FhirReference>();

            var rank = 1;
            foreach (var dx in diagnoses)
            {
                var conditionId = $"urn:uuid:{Guid.NewGuid()}";
                var condition = new IhceCondition
                {
                    Code = new FhirCodeableConcept
                    {
                        Coding = { new FhirCoding { System = IhceCodeSystems.Cie10, Code = dx.Cie10Code, Display = dx.Cie10Display } },
                        Text = dx.Cie10Display
                    },
                    Subject = new FhirReference { Reference = patientId }
                };

                conditionEntries.Add(new FhirBundleEntry { FullUrl = conditionId, Resource = condition });
                diagnosisRefs.Add(new IhceEncounterDiagnosis { Condition = new FhirReference { Reference = conditionId }, Rank = dx.IsMainDiagnosis ? 1 : ++rank });
                conditionRefsForSection.Add(new FhirReference { Reference = conditionId, Display = dx.Cie10Display });
            }

            var fhirEncounter = new IhceEncounter
            {
                ServiceType = new FhirCodeableConcept
                {
                    Coding = { new FhirCoding { System = IhceCodeSystems.Cups, Code = encounter.CupsServiceCode, Display = encounter.CupsServiceDisplay } }
                },
                Subject = new FhirReference { Reference = patientId },
                Period = new FhirPeriod { Start = encounter.AttentionDate.ToString("yyyy-MM-ddTHH:mm:sszzz") },
                ReasonCode = { new FhirCodeableConcept { Text = encounter.CausaMotivoAtencionDisplay } },
                Diagnosis = diagnosisRefs
            };

            var composition = new IhceComposition
            {
                Subject = new FhirReference { Reference = patientId },
                Encounter = new FhirReference { Reference = encounterId },
                Date = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:sszzz"),
                Section =
                {
                    new IhceCompositionSection { Title = "Diagnósticos", Entry = conditionRefsForSection }
                }
            };

            var bundle = new FhirBundle { Timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:sszzz") };
            bundle.Entry.Add(new FhirBundleEntry { FullUrl = $"urn:uuid:{Guid.NewGuid()}", Resource = composition });
            bundle.Entry.Add(new FhirBundleEntry { FullUrl = patientId, Resource = fhirPatient });
            bundle.Entry.Add(new FhirBundleEntry { FullUrl = encounterId, Resource = fhirEncounter });
            bundle.Entry.AddRange(conditionEntries);

            return bundle;
        }
    }
}
