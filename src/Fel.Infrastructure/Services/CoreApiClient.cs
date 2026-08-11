using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Fel.Core.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Fel.Infrastructure.Services
{
    /// <summary>
    /// Cliente HTTP para comunicarse con la base Core del ecosistema FacilApps
    /// (Supabase). Usa la service role key (bypass de RLS) para lectura de países
    /// disponibles y creación de tenants.
    /// </summary>
    public class CoreApiClient : ICoreApiClient
    {
        private readonly HttpClient _http;
        private readonly ILogger<CoreApiClient> _logger;
        private readonly string _supabaseUrl;
        private readonly string _serviceRoleKey;
        private readonly string _platformId;
        private const string _defaultCountryId = "4a2b129d-85cd-4069-97e2-2aafd96d5b05"; // Colombia en Core

        public CoreApiClient(HttpClient http, IConfiguration config, ILogger<CoreApiClient> logger)
        {
            _http = http;
            _logger = logger;

            _supabaseUrl = (config["Core:SupabaseUrl"] ?? config["Core__SupabaseUrl"] ?? string.Empty).TrimEnd('/');
            _serviceRoleKey = config["Core:ServiceRoleKey"] ?? config["Core__ServiceRoleKey"] ?? string.Empty;
            _platformId = config["Core:PlatformId"] ?? config["Core__PlatformId"] ?? string.Empty;
        }

        private bool IsConfigured => !string.IsNullOrWhiteSpace(_supabaseUrl)
                                     && !string.IsNullOrWhiteSpace(_serviceRoleKey)
                                     && !string.IsNullOrWhiteSpace(_platformId);

        public async Task<IReadOnlyList<CoreCountry>> GetPlatformCountriesAsync(CancellationToken ct = default)
        {
            if (!IsConfigured)
            {
                _logger.LogWarning("Core not configured; returning empty platform countries.");
                return Array.Empty<CoreCountry>();
            }

            var url = $"{_supabaseUrl}/rest/v1/platform_countries?select=platform_id,country_id,countries(iso_code,name)&platform_id=eq.{_platformId}";

            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            AddAuth(req);

            using var resp = await _http.SendAsync(req, ct);
            resp.EnsureSuccessStatusCode();

            var json = await resp.Content.ReadAsStringAsync(ct);
            var rows = JsonSerializer.Deserialize<List<PlatformCountryRow>>(json, JsonOpts);

            var result = new List<CoreCountry>();
            foreach (var r in rows ?? new List<PlatformCountryRow>())
            {
                if (r.Countries is not null)
                {
                    result.Add(new CoreCountry
                    {
                        Id = string.IsNullOrWhiteSpace(r.Countries?.Id) ? r.CountryId : r.Countries.Id,
                        IsoCode = r.Countries?.IsoCode ?? string.Empty,
                        Name = r.Countries?.Name ?? string.Empty
                    });
                }
            }
            return result;
        }

        private string ResolveSlug(CoreTenantCreate tenant)
        {
            var slug = tenant.Slug;
            if (string.IsNullOrWhiteSpace(slug))
            {
                slug = tenant.Name.ToLowerInvariant()
                    .Replace(" ", "-")
                    .Trim('-');
            }
            return slug;
        }

        private string ResolveCountryId(string? countryId)
            => string.IsNullOrWhiteSpace(countryId) ? _defaultCountryId : countryId;

        public async Task<CoreResult<CoreTenant?>> FindTenantAsync(string slug, string? countryId, CancellationToken ct = default)
        {
            if (!IsConfigured)
            {
                _logger.LogWarning("Core not configured; skipping tenant lookup.");
                return CoreResult<CoreTenant?>.NotConfigured();
            }

            var country = ResolveCountryId(countryId);
            var url = $"{_supabaseUrl}/rest/v1/tenants" +
                      $"?select=id,name,slug,platform_id" +
                      $"&platform_id=eq.{_platformId}&country_id=eq.{country}&slug=eq.{Uri.EscapeDataString(slug)}";

            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            AddAuth(req);

            try
            {
                using var resp = await _http.SendAsync(req, ct);
                var json = await resp.Content.ReadAsStringAsync(ct);

                if (!resp.IsSuccessStatusCode)
                {
                    _logger.LogError("Core find tenant failed ({Code}): {Body}", (int)resp.StatusCode, json);
                    return CoreResult<CoreTenant?>.Fail(json, (int)resp.StatusCode);
                }

                var rows = JsonSerializer.Deserialize<List<CoreTenantRow>>(json, JsonOpts) ?? new List<CoreTenantRow>();
                if (rows.Count == 0) return CoreResult<CoreTenant?>.Ok(null);

                var r = rows[0];
                return CoreResult<CoreTenant?>.Ok(new CoreTenant
                {
                    Id = r.Id,
                    Name = r.Name,
                    Slug = r.Slug,
                    PlatformId = r.PlatformId
                });
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Core find tenant threw.");
                return CoreResult<CoreTenant?>.Fail(ex.Message);
            }
        }

        public async Task<CoreResult<CoreTenant>> CreateTenantAsync(CoreTenantCreate tenant, CancellationToken ct = default)
        {
            if (!IsConfigured)
            {
                _logger.LogWarning("Core not configured. Skipping tenant creation in Core.");
                return CoreResult<CoreTenant>.NotConfigured();
            }

            var slug = ResolveSlug(tenant);

            // Idempotencia: si la ficha ya existe con el mismo (platform_id, country_id, slug),
            // adoptarla. Un insert violaria unique_platform_country_slug.
            var existing = await FindTenantAsync(slug, tenant.CountryId, ct);
            if (existing.IsFailed)
            {
                return CoreResult<CoreTenant>.Fail(existing.Error ?? "lookup failed", existing.StatusCode);
            }
            if (existing.IsSuccess && existing.Value is not null)
            {
                _logger.LogInformation(
                    "Core tenant with slug {Slug} already exists ({Id}); adopting instead of creating.",
                    slug, existing.Value.Id);
                existing.Value.Adopted = true;
                return CoreResult<CoreTenant>.Ok(existing.Value);
            }

            var payload = new
            {
                name = tenant.Name,
                slug,
                legal_name = tenant.LegalName,
                tax_id = tenant.TaxId,
                contact_phone = tenant.ContactPhone,
                whatsapp_phone = tenant.WhatsAppPhone,
                einvoicing_email = tenant.EinvoicingEmail,
                commercial_email = tenant.CommercialEmail,
                website = tenant.Website,
                physical_address_line1 = tenant.PhysicalAddressLine1,
                physical_address_line2 = tenant.PhysicalAddressLine2,
                physical_city = tenant.PhysicalCity,
                physical_state = tenant.PhysicalState,
                physical_postal_code = tenant.PhysicalPostalCode,
                billing_address = tenant.BillingAddress,
                default_language_code = tenant.DefaultLanguageCode,
                default_timezone = tenant.DefaultTimezone,
                default_currency_id = tenant.DefaultCurrencyId,
                country_id = ResolveCountryId(tenant.CountryId),
                latitude = tenant.Latitude,
                longitude = tenant.Longitude,
                platform_id = _platformId,
                @is_active = true,
                subscription_status = "trial"
            };
            var url = $"{_supabaseUrl}/rest/v1/tenants";

            using var req = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(JsonSerializer.Serialize(payload, JsonOpts), Encoding.UTF8, "application/json")
            };
            req.Headers.TryAddWithoutValidation("Prefer", "return=representation");
            AddAuth(req);

            try
            {
                using var resp = await _http.SendAsync(req, ct);
                var json = await resp.Content.ReadAsStringAsync(ct);

                if (!resp.IsSuccessStatusCode)
                {
                    _logger.LogError("Core create tenant failed ({Code}): {Body}", (int)resp.StatusCode, json);
                    return CoreResult<CoreTenant>.Fail(json, (int)resp.StatusCode);
                }

                var list = JsonSerializer.Deserialize<List<CoreTenantRow>>(json, JsonOpts) ?? new List<CoreTenantRow>();
                var row = list.Count > 0 ? list[0] : null;
                if (row is null)
                {
                    _logger.LogError("Core create tenant returned an empty representation.");
                    return CoreResult<CoreTenant>.Fail("Core devolvió una representación vacía al crear el tenant.");
                }

                return CoreResult<CoreTenant>.Ok(new CoreTenant
                {
                    Id = row.Id,
                    Name = row.Name,
                    Slug = row.Slug,
                    PlatformId = row.PlatformId
                });
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Core create tenant threw.");
                return CoreResult<CoreTenant>.Fail(ex.Message);
            }
        }

        public async Task<CoreResult<CoreTenant>> UpdateTenantAsync(string coreTenantId, CoreTenantCreate tenant, CancellationToken ct = default)
        {
            if (!IsConfigured)
            {
                _logger.LogWarning("Core not configured. Skipping tenant update in Core.");
                return CoreResult<CoreTenant>.NotConfigured();
            }

            // No se tocan platform_id, country_id, slug ni is_system_owner: definen la identidad
            // de la ficha en Core y su unicidad por plataforma.
            var payload = new
            {
                name = tenant.Name,
                legal_name = tenant.LegalName,
                tax_id = tenant.TaxId,
                contact_phone = tenant.ContactPhone,
                whatsapp_phone = tenant.WhatsAppPhone,
                einvoicing_email = tenant.EinvoicingEmail,
                commercial_email = tenant.CommercialEmail,
                website = tenant.Website,
                physical_address_line1 = tenant.PhysicalAddressLine1,
                physical_address_line2 = tenant.PhysicalAddressLine2,
                physical_city = tenant.PhysicalCity,
                physical_state = tenant.PhysicalState,
                physical_postal_code = tenant.PhysicalPostalCode,
                billing_address = tenant.BillingAddress,
                default_language_code = tenant.DefaultLanguageCode,
                default_timezone = tenant.DefaultTimezone,
                default_currency_id = tenant.DefaultCurrencyId,
                latitude = tenant.Latitude,
                longitude = tenant.Longitude
            };

            var url = $"{_supabaseUrl}/rest/v1/tenants?id=eq.{Uri.EscapeDataString(coreTenantId)}";
            using var req = new HttpRequestMessage(HttpMethod.Patch, url)
            {
                Content = new StringContent(JsonSerializer.Serialize(payload, JsonOpts), Encoding.UTF8, "application/json")
            };
            req.Headers.TryAddWithoutValidation("Prefer", "return=representation");
            AddAuth(req);

            try
            {
                using var resp = await _http.SendAsync(req, ct);
                var json = await resp.Content.ReadAsStringAsync(ct);

                if (!resp.IsSuccessStatusCode)
                {
                    _logger.LogError("Core update tenant failed ({Code}): {Body}", (int)resp.StatusCode, json);
                    return CoreResult<CoreTenant>.Fail(json, (int)resp.StatusCode);
                }

                var list = JsonSerializer.Deserialize<List<CoreTenantRow>>(json, JsonOpts) ?? new List<CoreTenantRow>();
                var row = list.Count > 0 ? list[0] : null;
                if (row is null)
                {
                    // PATCH sin filas afectadas: el id no existe en Core (puntero colgante).
                    _logger.LogError("Core update tenant {Id} affected no rows.", coreTenantId);
                    return CoreResult<CoreTenant>.Fail($"La ficha {coreTenantId} no existe en Core.", 404);
                }

                return CoreResult<CoreTenant>.Ok(new CoreTenant
                {
                    Id = row.Id,
                    Name = row.Name,
                    Slug = row.Slug,
                    PlatformId = row.PlatformId
                });
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Core update tenant threw.");
                return CoreResult<CoreTenant>.Fail(ex.Message);
            }
        }

        public async Task<CoreResult<bool>> DeleteTenantAsync(string coreTenantId, CancellationToken ct = default)
        {
            if (!IsConfigured)
            {
                _logger.LogWarning("Core not configured. Skipping tenant delete in Core.");
                return CoreResult<bool>.NotConfigured();
            }

            var url = $"{_supabaseUrl}/rest/v1/tenants?id=eq.{Uri.EscapeDataString(coreTenantId)}";
            using var req = new HttpRequestMessage(HttpMethod.Delete, url);
            AddAuth(req);

            try
            {
                using var resp = await _http.SendAsync(req, ct);
                if (!resp.IsSuccessStatusCode)
                {
                    var json = await resp.Content.ReadAsStringAsync(ct);
                    _logger.LogError("Core delete tenant failed ({Code}): {Body}", (int)resp.StatusCode, json);
                    return CoreResult<bool>.Fail(json, (int)resp.StatusCode);
                }
                return CoreResult<bool>.Ok(true);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Core delete tenant threw.");
                return CoreResult<bool>.Fail(ex.Message);
            }
        }

        public async Task<CoreResult<IReadOnlyList<string>>> GetExistingTenantIdsAsync(IEnumerable<string> ids, CancellationToken ct = default)
        {
            if (!IsConfigured)
            {
                _logger.LogWarning("Core not configured; skipping tenant existence check.");
                return CoreResult<IReadOnlyList<string>>.NotConfigured();
            }

            var list = ids.Where(i => !string.IsNullOrWhiteSpace(i)).Distinct().ToList();
            if (list.Count == 0)
            {
                return CoreResult<IReadOnlyList<string>>.Ok(Array.Empty<string>());
            }

            var inClause = string.Join(",", list.Select(Uri.EscapeDataString));
            var url = $"{_supabaseUrl}/rest/v1/tenants?select=id&id=in.({inClause})";

            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            AddAuth(req);

            try
            {
                using var resp = await _http.SendAsync(req, ct);
                var json = await resp.Content.ReadAsStringAsync(ct);

                if (!resp.IsSuccessStatusCode)
                {
                    _logger.LogError("Core tenant existence check failed ({Code}): {Body}", (int)resp.StatusCode, json);
                    return CoreResult<IReadOnlyList<string>>.Fail(json, (int)resp.StatusCode);
                }

                var rows = JsonSerializer.Deserialize<List<CoreTenantRow>>(json, JsonOpts) ?? new List<CoreTenantRow>();
                return CoreResult<IReadOnlyList<string>>.Ok(rows.Select(r => r.Id).ToList());
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Core tenant existence check threw.");
                return CoreResult<IReadOnlyList<string>>.Fail(ex.Message);
            }
        }

        public async Task<CoreRegistrationData?> GetRegistrationDataAsync(CancellationToken ct = default)
        {
            if (!IsConfigured)
            {
                _logger.LogWarning("Core not configured; returning null registration data.");
                return null;
            }

            var countriesUrl = $"{_supabaseUrl}/rest/v1/platform_countries?select=platform_id,country_id,countries(id,name,iso_code,default_currency_id,default_language_iso_code,default_localization_id,default_latitude,default_longitude,timezones)&platform_id=eq.{_platformId}";

            using var reqCountries = new HttpRequestMessage(HttpMethod.Get, countriesUrl);
            AddAuth(reqCountries);
            using var respCountries = await _http.SendAsync(reqCountries, ct);
            respCountries.EnsureSuccessStatusCode();
            var countriesJson = await respCountries.Content.ReadAsStringAsync(ct);

            var countryRows = JsonSerializer.Deserialize<List<PlatformCountryRowFull>>(countriesJson, JsonOpts) ?? new();
            var countries = new List<CoreCountry>();
            foreach (var r in countryRows)
            {
                if (r.Countries is null) continue;
                countries.Add(new CoreCountry
                {
                    Id = r.Countries.Id,
                    IsoCode = r.Countries.IsoCode,
                    Name = r.Countries.Name,
                    DefaultCurrencyId = r.Countries.DefaultCurrencyId,
                    DefaultLanguageIsoCode = r.Countries.DefaultLanguageIsoCode,
                    DefaultLocalizationId = r.Countries.DefaultLocalizationId,
                    Timezones = r.Countries.Timezones ?? Array.Empty<string>(),
                    DefaultLatitude = r.Countries.DefaultLatitude,
                    DefaultLongitude = r.Countries.DefaultLongitude
                });
            }

            // Idiomas
            var languages = await FetchListAsync<LanguageRowFull>(
                $"{_supabaseUrl}/rest/v1/languages?select=id,name,iso_code&is_active=eq.true",
                ct);
            var languagesMapped = languages.Select(l => new CoreLanguage
            {
                Id = l.Id,
                Name = l.Name,
                IsoCode = l.IsoCode
            }).ToList();

            // Monedas
            var currencies = await FetchListAsync<CoreCurrency>(
                $"{_supabaseUrl}/rest/v1/currencies?select=id,name,code,symbol&is_active=eq.true",
                ct);

            return new CoreRegistrationData
            {
                Countries = countries,
                Languages = languagesMapped,
                Currencies = currencies
            };
        }

        private async Task<IReadOnlyList<T>> FetchListAsync<T>(string url, CancellationToken ct)
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            AddAuth(req);
            using var resp = await _http.SendAsync(req, ct);
            resp.EnsureSuccessStatusCode();
            var json = await resp.Content.ReadAsStringAsync(ct);
            return JsonSerializer.Deserialize<List<T>>(json, JsonOpts) ?? new List<T>();
        }

        private void AddAuth(HttpRequestMessage req)
        {
            req.Headers.TryAddWithoutValidation("apikey", _serviceRoleKey);
            req.Headers.TryAddWithoutValidation("Authorization", $"Bearer {_serviceRoleKey}");
        }

        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        private class PlatformCountryRow
        {
            [JsonPropertyName("platform_id")]
            public string PlatformId { get; set; } = string.Empty;
            [JsonPropertyName("country_id")]
            public string CountryId { get; set; } = string.Empty;
            public CountriesEmbedded? Countries { get; set; }
        }

        private class CountriesEmbedded
        {
            [JsonPropertyName("id")]
            public string Id { get; set; } = string.Empty;
            [JsonPropertyName("iso_code")]
            public string IsoCode { get; set; } = string.Empty;
            [JsonPropertyName("name")]
            public string Name { get; set; } = string.Empty;
        }

        private class CoreTenantRow
        {
            [JsonPropertyName("id")]
            public string Id { get; set; } = string.Empty;
            [JsonPropertyName("name")]
            public string Name { get; set; } = string.Empty;
            [JsonPropertyName("slug")]
            public string? Slug { get; set; }
            [JsonPropertyName("platform_id")]
            public string? PlatformId { get; set; }
        }

        private class PlatformCountryRowFull
        {
            [JsonPropertyName("country_id")]
            public string CountryId { get; set; } = string.Empty;
            public CountriesEmbeddedFull? Countries { get; set; }
        }

        private class LanguageRowFull
        {
            [JsonPropertyName("id")]
            public string Id { get; set; } = string.Empty;
            [JsonPropertyName("name")]
            public string Name { get; set; } = string.Empty;
            [JsonPropertyName("iso_code")]
            public string IsoCode { get; set; } = string.Empty;
        }

        private class CountriesEmbeddedFull
        {
            [JsonPropertyName("id")]
            public string Id { get; set; } = string.Empty;
            [JsonPropertyName("iso_code")]
            public string IsoCode { get; set; } = string.Empty;
            [JsonPropertyName("name")]
            public string Name { get; set; } = string.Empty;
            [JsonPropertyName("default_currency_id")]
            public string? DefaultCurrencyId { get; set; }
            [JsonPropertyName("default_language_iso_code")]
            public string? DefaultLanguageIsoCode { get; set; }
            [JsonPropertyName("default_localization_id")]
            public string? DefaultLocalizationId { get; set; }
            [JsonPropertyName("default_latitude")]
            public double? DefaultLatitude { get; set; }
            [JsonPropertyName("default_longitude")]
            public double? DefaultLongitude { get; set; }
            [JsonPropertyName("timezones")]
            public string[]? Timezones { get; set; }
        }
    }
}