using System;

namespace Fel.Infrastructure.Services
{
    // Datos fiscales leídos del RUT al registrar un tenant. El formulario de alta no los muestra todos, pero los conserva y los envía al crear: así el
    // tenant y su empresa propietaria nacen con lo fiscal lleno. Todos son opcionales: un tenant sin RUT nace con lo que traiga el formulario.
    public class TenantRutData
    {
        public string? TaxId { get; set; }
        public string? VerificationDigit { get; set; }
        // "PJ" o "PN".
        public string? PersonType { get; set; }
        public string? CommercialName { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string? Address { get; set; }
        public string? City { get; set; }
        // Código DANE del municipio.
        public string? CityCode { get; set; }
        public string? Department { get; set; }
        public string? EconomicActivity { get; set; }
        // Texto del régimen para el tenant ("Responsable de IVA"...) y código DIAN para la empresa propietaria ("48" o "49").
        public string? TaxRegime { get; set; }
        public string? TaxRegimeCode { get; set; }
        public string? IncomeTaxRegime { get; set; }
        public bool IsGranContribuyente { get; set; }
        public bool IsAgenteRetenedorIva { get; set; }
        public bool IsAutorretenedorRenta { get; set; }

        public string? LegalRepresentativeFirstName { get; set; }
        public string? LegalRepresentativeOtherNames { get; set; }
        public string? LegalRepresentativeFirstLastName { get; set; }
        public string? LegalRepresentativeSecondLastName { get; set; }
        public string? LegalRepresentativeDocumentType { get; set; }
        public string? LegalRepresentativeDocumentNumber { get; set; }
        public string? LegalRepresentativeRepresentationCode { get; set; }
        public DateTime? LegalRepresentativeStartDate { get; set; }
    }
}
