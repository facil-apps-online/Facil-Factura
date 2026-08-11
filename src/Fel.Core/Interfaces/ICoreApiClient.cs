namespace Fel.Core.Interfaces
{
    public class CoreCountry
    {
        public string? Id { get; set; }
        public string IsoCode { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? DefaultCurrencyId { get; set; }
        public string? DefaultLanguageIsoCode { get; set; }
        public string? DefaultLocalizationId { get; set; }
        public IReadOnlyList<string> Timezones { get; set; } = Array.Empty<string>();
        public double? DefaultLatitude { get; set; }
        public double? DefaultLongitude { get; set; }
    }

    public class CoreLanguage
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string IsoCode { get; set; } = string.Empty;
    }

    public class CoreCurrency
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public string? Symbol { get; set; }
    }

    public class CoreRegistrationData
    {
        public IReadOnlyList<CoreCountry> Countries { get; set; } = Array.Empty<CoreCountry>();
        public IReadOnlyList<CoreLanguage> Languages { get; set; } = Array.Empty<CoreLanguage>();
        public IReadOnlyList<CoreCurrency> Currencies { get; set; } = Array.Empty<CoreCurrency>();
    }

    public class CoreTenant
    {
        public string? Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Slug { get; set; }
        public string? PlatformId { get; set; }

        /// <summary>
        /// True si la ficha ya existía en Core y se adoptó, en vez de crearse ahora.
        /// </summary>
        public bool Adopted { get; set; }
    }

    public enum CoreOutcome
    {
        /// <summary>La operación se completó contra Core.</summary>
        Success,

        /// <summary>
        /// Core no está configurado (falta SupabaseUrl, ServiceRoleKey o PlatformId).
        /// Es un estado legítimo en desarrollo local: no debe tratarse como error.
        /// </summary>
        NotConfigured,

        /// <summary>Core respondió con error o no se pudo contactar.</summary>
        Failed
    }

    /// <summary>
    /// Resultado de una operación contra Core. Distingue el fallo real del "Core no
    /// configurado", que antes se confundían porque ambos devolvían null.
    /// </summary>
    public class CoreResult<T>
    {
        public CoreOutcome Outcome { get; init; }
        public T? Value { get; init; }
        public int? StatusCode { get; init; }
        public string? Error { get; init; }

        public bool IsSuccess => Outcome == CoreOutcome.Success;
        public bool IsNotConfigured => Outcome == CoreOutcome.NotConfigured;
        public bool IsFailed => Outcome == CoreOutcome.Failed;

        public static CoreResult<T> Ok(T value) => new() { Outcome = CoreOutcome.Success, Value = value };
        public static CoreResult<T> NotConfigured() => new() { Outcome = CoreOutcome.NotConfigured };
        public static CoreResult<T> Fail(string error, int? statusCode = null)
            => new() { Outcome = CoreOutcome.Failed, Error = error, StatusCode = statusCode };
    }

    public class CoreTenantCreate
    {
        public string Name { get; set; } = string.Empty;
        public string? Slug { get; set; }
        public string? LegalName { get; set; }
        public string? TaxId { get; set; }
        public string? ContactPhone { get; set; }
        public string? WhatsAppPhone { get; set; }
        public string? EinvoicingEmail { get; set; }
        public string? CommercialEmail { get; set; }
        public string? Website { get; set; }
        public string? PhysicalAddressLine1 { get; set; }
        public string? PhysicalAddressLine2 { get; set; }
        public string? PhysicalCity { get; set; }
        public string? PhysicalState { get; set; }
        public string? PhysicalPostalCode { get; set; }
        public string? BillingAddress { get; set; }
        public string? DefaultLanguageCode { get; set; }
        public string? DefaultTimezone { get; set; }
        public string? DefaultCurrencyId { get; set; }
        public string? CountryId { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
    }

    public interface ICoreApiClient
    {
        /// <summary>
        /// Devuelve los países habilitados para la plataforma de Facil Factura en Core
        /// (platform_countries), usados para mostrar disponibilidad del servicio por ubicación.
        /// </summary>
        Task<IReadOnlyList<CoreCountry>> GetPlatformCountriesAsync(CancellationToken ct = default);

        /// <summary>
        /// Devuelve los datos públicos necesarios para el registro: países de la plataforma
        /// (con idioma, moneda y zonas horarias por defecto) + idiomas + monedas.
        /// </summary>
        Task<CoreRegistrationData?> GetRegistrationDataAsync(CancellationToken ct = default);

        /// <summary>
        /// Asegura el registro comercial del tenant en Core (tabla tenants), manteniendo la
        /// dualidad entre el tenant local (FelDb) y Core.
        /// Si ya existe una ficha con el mismo (platform_id, country_id, slug) la adopta y la
        /// devuelve con <see cref="CoreTenant.Adopted"/> a true, en vez de intentar un insert
        /// que violaría unique_platform_country_slug.
        /// </summary>
        Task<CoreResult<CoreTenant>> CreateTenantAsync(CoreTenantCreate tenant, CancellationToken ct = default);

        /// <summary>
        /// Busca una ficha por (platform_id, country_id, slug). Value es null si no existe.
        /// </summary>
        Task<CoreResult<CoreTenant?>> FindTenantAsync(string slug, string? countryId, CancellationToken ct = default);

        /// <summary>
        /// Actualiza en Core los datos comerciales de una ficha ya enlazada.
        /// </summary>
        Task<CoreResult<CoreTenant>> UpdateTenantAsync(string coreTenantId, CoreTenantCreate tenant, CancellationToken ct = default);

        /// <summary>
        /// Borra una ficha de Core. Se usa como acción compensatoria cuando el alta se
        /// confirmó en Core pero falló el commit local.
        /// </summary>
        Task<CoreResult<bool>> DeleteTenantAsync(string coreTenantId, CancellationToken ct = default);

        /// <summary>
        /// De los ids dados, devuelve los que realmente existen en Core. Permite detectar
        /// punteros colgantes: CoreTenantId es un nvarchar suelto y al ser bases distintas
        /// no puede haber clave foránea que lo garantice.
        /// </summary>
        Task<CoreResult<IReadOnlyList<string>>> GetExistingTenantIdsAsync(IEnumerable<string> ids, CancellationToken ct = default);
    }
}