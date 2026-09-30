using System.Globalization;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Fel.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace Fel.Infrastructure.Certificates;

public sealed class ViafirmaPkcs10Provider : ICertificateProvider
{
    private const string ProfilesPath = "/ra/available-profiles";
    private const string RequestPath = "/request/fromCSR";
    private const string StatusPath = "/request/{0}/status";
    private const string AdvancedStatusPath = "/request/{0}/advancedStatus";
    private const string KycPath = "/services/accreditation/{0}";
    private const string UploadFilesPath = "/files/upload/";
    private const string ListFilesPath = "/files/list/{0}";
    private const string RevocationCodePath = "/request/{0}/revocationCode";
    private const string RevokePath = "/request/revoke/code/{0}";
    private const string RejectPath = "/request/reject/{0}";

    private readonly HttpClient _httpClient;
    private readonly ILogger<ViafirmaPkcs10Provider> _logger;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    public ViafirmaPkcs10Provider(HttpClient httpClient, ILogger<ViafirmaPkcs10Provider> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<IReadOnlyList<ProviderCertificateProfile>> GetProfilesAsync(
        CertificateProviderContext context,
        CancellationToken cancellationToken = default)
    {
        using var response = await SendAsync(HttpMethod.Get, ProfilesPath + "?codRa=" + Encode(context.RaCode), null, context, cancellationToken);
        using var document = await ReadJsonAsync(response, cancellationToken);
        var result = new List<ProviderCertificateProfile>();

        if (!document.RootElement.TryGetProperty("items", out var items))
            return result;

        foreach (var item in items.EnumerateArray())
        {
            result.Add(new ProviderCertificateProfile(
                GetString(item, "code"),
                GetString(item, "title"),
                GetString(item, "description"),
                GetString(item, "propertyRfc822"),
                GetNullableInt(item, "validity"),
                GetString(item, "ra"),
                GetString(item, "type"),
                GetString(item, "token"),
                GetString(item, "terms")));
        }

        return result;
    }

    public async Task<IReadOnlyList<ProviderProfileField>> GetProfileFieldsAsync(
        string profileCode,
        CertificateProviderContext context,
        CancellationToken cancellationToken = default)
    {
        var profilePaths = new List<string>
        {
            $"/ra/profile/{profileCode}/form?required=true",
            $"/ra/profile/{Encode(profileCode)}/form?required=true"
        };

        if (TryDecodeProfileCode(profileCode, out var decodedProfileCode))
        {
            profilePaths.Add($"/ra/profile/{decodedProfileCode}/form?required=true");
            profilePaths.Add($"/ra/profile/{Encode(decodedProfileCode)}/form?required=true");
            profilePaths.Add($"/profile/{decodedProfileCode}/form?required=true");
            profilePaths.Add($"/profile/{Encode(decodedProfileCode)}/form?required=true");
        }

        JsonDocument? document = null;
        CertificateProviderException? lastProviderException = null;
        foreach (var path in profilePaths.Distinct(StringComparer.Ordinal))
        {
            try
            {
                using var response = await SendAsync(HttpMethod.Get, path, null, context, cancellationToken);
                document = await ReadJsonAsync(response, cancellationToken);
                break;
            }
            catch (CertificateProviderException ex) when (ex.Code == "provider_invalid_json")
            {
                lastProviderException = ex;
            }
        }

        if (document == null)
        {
            throw lastProviderException ?? new CertificateProviderException(
                "provider_invalid_json",
                "Viafirma no devolvió un formulario JSON válido para el perfil.");
        }

        using (document)
        {
        var result = new List<ProviderProfileField>();
        var root = document.RootElement;
        var items = root.ValueKind == JsonValueKind.Array
            ? root
            : root.TryGetProperty("items", out var nested) ? nested : default;

        if (items.ValueKind != JsonValueKind.Array)
            return result;

        foreach (var item in items.EnumerateArray())
        {
            result.Add(new ProviderProfileField(
                GetString(item, "name"),
                GetString(item, "label"),
                GetString(item, "type"),
                GetString(item, "validate"),
                GetString(item, "defaultValue"),
                GetBoolean(item, "required"),
                GetBoolean(item, "editable"),
                GetBoolean(item, "used"),
                GetInt(item, "index")));
        }

        return result;
        }
    }

    public async Task<ProviderRequestCreated> CreateRequestFromCsrAsync(
        CreateProviderRequest command,
        CertificateProviderContext context,
        CancellationToken cancellationToken = default)
    {
        var payload = new Dictionary<string, string>
        {
            ["identityType"] = command.IdentityType,
            ["countryCode"] = command.CountryCode,
            ["identity"] = command.Identity,
            ["ra"] = command.RaCode,
            ["codProfile"] = command.ProfileCode,
            ["emailCertificate"] = command.CertificateEmail,
            ["organizationType"] = command.OrganizationType,
            ["csr"] = command.CsrBase64
        };

        using var response = await SendAsync(HttpMethod.Post, RequestPath, JsonContent.Create(payload), context, cancellationToken);
        using var document = await ReadJsonAsync(response, cancellationToken);
        return new ProviderRequestCreated(GetString(document.RootElement, "codRequest"), GetString(document.RootElement, "publicId"));
    }

    public async Task<ProviderRequestStatus> GetStatusAsync(string requestCode, CertificateProviderContext context, CancellationToken cancellationToken = default)
    {
        using var response = await SendAsync(HttpMethod.Get, string.Format(CultureInfo.InvariantCulture, StatusPath, Encode(requestCode)), null, context, cancellationToken);
        using var document = await ReadJsonAsync(response, cancellationToken);
        return new ProviderRequestStatus(GetString(document.RootElement, "code"));
    }

    public async Task<ProviderAdvancedStatus> GetAdvancedStatusAsync(string requestCode, CertificateProviderContext context, CancellationToken cancellationToken = default)
    {
        using var response = await SendAsync(HttpMethod.Get, string.Format(CultureInfo.InvariantCulture, AdvancedStatusPath, Encode(requestCode)), null, context, cancellationToken);
        using var document = await ReadJsonAsync(response, cancellationToken);
        var root = document.RootElement;
        var notes = new List<ProviderPublicNote>();
        if (root.TryGetProperty("requestPublicNotes", out var rawNotes) && rawNotes.ValueKind == JsonValueKind.Array)
        {
            foreach (var note in rawNotes.EnumerateArray())
            {
                DateTime? date = null;
                if (note.TryGetProperty("date", out var dateValue) && dateValue.TryGetInt64(out var epoch))
                    date = DateTimeOffset.FromUnixTimeMilliseconds(epoch).UtcDateTime;
                notes.Add(new ProviderPublicNote(GetString(note, "note"), date));
            }
        }

        return new ProviderAdvancedStatus(
            GetString(root, "status"),
            GetString(root, "accredited"),
            GetString(root, "paid"),
            notes);
    }

    public async Task<Uri> GetKycLinkAsync(string requestCode, CertificateProviderContext context, CancellationToken cancellationToken = default)
    {
        using var response = await SendAsync(HttpMethod.Get, string.Format(CultureInfo.InvariantCulture, KycPath, Encode(requestCode)), null, context, cancellationToken);
        using var document = await ReadJsonAsync(response, cancellationToken);
        var link = GetString(document.RootElement, "link");
        if (!Uri.TryCreate(link, UriKind.Absolute, out var uri))
            throw new CertificateProviderException("provider_invalid_kyc_link", "Viafirma no devolvió un enlace KYC válido.");
        return uri;
    }

    public async Task<IReadOnlyList<ProviderFile>> UploadFilesAsync(string requestCode, IReadOnlyList<ProviderFileUpload> files, CertificateProviderContext context, CancellationToken cancellationToken = default)
    {
        var payload = new
        {
            codeRequest = requestCode,
            files = files.Select(file => new { name = file.Name, base64 = Convert.ToBase64String(file.Content) })
        };
        using var response = await SendAsync(HttpMethod.Post, UploadFilesPath, JsonContent.Create(payload), context, cancellationToken);
        return await ReadFilesAsync(response, cancellationToken);
    }

    public async Task<IReadOnlyList<ProviderFile>> ListFilesAsync(string requestCode, CertificateProviderContext context, CancellationToken cancellationToken = default)
    {
        using var response = await SendAsync(HttpMethod.Get, string.Format(CultureInfo.InvariantCulture, ListFilesPath, Encode(requestCode)), null, context, cancellationToken);
        return await ReadFilesAsync(response, cancellationToken);
    }

    public async Task<byte[]> DownloadP7bAsync(string publicId, CertificateProviderContext context, CancellationToken cancellationToken = default)
    {
        // SendAsync ya combina el path con context.DownloadBaseUrl (useDownloadBaseUrl: true) — pasar
        // acá una ruta ya absoluta (construida a mano con el mismo DownloadBaseUrl) duplicaba el
        // prefijo (ej. /ra/api/v2/ra/api/v2/...), coincidiendo por error con alguna página del
        // servidor que respondía 200 sin ser el .p7b real.
        using var response = await SendAsync(HttpMethod.Get, "downloadCertificateServlet?req=" + Encode(publicId), null, context, cancellationToken, useDownloadBaseUrl: true);
        var rawBytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);

        // El manual documenta que este método devuelve el P7B en base64, pero en Sandbox (Viafirma
        // RA v3.4.71) el servidor entrega directamente el binario DER del PKCS#7 — solo se decodifica
        // como base64 cuando el contenido realmente tiene esa forma; si no, ya es el P7B binario.
        var asText = Encoding.ASCII.GetString(rawBytes).Trim().Trim('"');
        if (IsLikelyBase64(asText))
        {
            try
            {
                return Convert.FromBase64String(asText);
            }
            catch (FormatException)
            {
            }
        }
        return rawBytes;
    }

    private static bool IsLikelyBase64(string value) =>
        value.Length > 0 && value.Length % 4 == 0 &&
        value.All(c => (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c is '+' or '/' or '=');

    public async Task<string> GetRevocationCodeAsync(string requestCode, CertificateProviderContext context, CancellationToken cancellationToken = default)
    {
        using var response = await SendAsync(HttpMethod.Get, string.Format(CultureInfo.InvariantCulture, RevocationCodePath, Encode(requestCode)), null, context, cancellationToken);
        using var document = await ReadJsonAsync(response, cancellationToken);
        return GetString(document.RootElement, "revocationCode");
    }

    public async Task RevokeAsync(string revocationCode, CertificateProviderContext context, CancellationToken cancellationToken = default)
    {
        using var response = await SendAsync(HttpMethod.Post, string.Format(CultureInfo.InvariantCulture, RevokePath, Encode(revocationCode)), null, context, cancellationToken);
        response.Dispose();
    }

    public async Task RejectAsync(string requestCode, CertificateProviderContext context, CancellationToken cancellationToken = default)
    {
        using var response = await SendAsync(HttpMethod.Put, string.Format(CultureInfo.InvariantCulture, RejectPath, Encode(requestCode)), null, context, cancellationToken);
        response.Dispose();
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, HttpContent? content, CertificateProviderContext? commandContext, CancellationToken cancellationToken, bool useDownloadBaseUrl = false, bool requireContext = false)
    {
        var context = commandContext;
        if (context == null && requireContext)
            throw new ArgumentException("El contexto del proveedor es obligatorio para esta operación.", nameof(commandContext));
        if (context == null)
            throw new ArgumentNullException(nameof(commandContext));

        var baseUrl = useDownloadBaseUrl ? context.DownloadBaseUrl : context.BaseUrl;
        var requestUri = new Uri(new Uri(baseUrl.TrimEnd('/') + "/"), path.TrimStart('/'));
        using var request = new HttpRequestMessage(method, requestUri) { Content = content };
        request.Headers.Authorization = new AuthenticationHeaderValue("OAuth", OAuth1Header(method.Method, requestUri, context.ConsumerKey, context.ConsumerSecret));

        var started = Stopwatch.GetTimestamp();
        var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        var elapsed = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        _logger.LogInformation("CertificateProviderCall Provider={Provider} Operation={Method} Path={Path} Status={Status} DurationMs={DurationMs}", context.ProviderKey, method.Method, path, (int)response.StatusCode, elapsed);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning("CertificateProviderCallFailed Provider={Provider} Path={Path} Status={Status} Body={Body}", context.ProviderKey, path, (int)response.StatusCode, body);
            response.Dispose();
            throw new CertificateProviderException(MapErrorCode(response.StatusCode), "La operación del proveedor de certificados no fue aceptada.", (int)response.StatusCode, body);
        }

        return response;
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        try
        {
            return JsonDocument.Parse(body);
        }
        catch (JsonException)
        {
            var snippet = body.Length > 300 ? body[..300] : body;
            throw new CertificateProviderException(
                "provider_invalid_json",
                $"Viafirma devolvió una respuesta no JSON. HTTP {(int)response.StatusCode}. Respuesta: {snippet}",
                (int)response.StatusCode,
                snippet);
        }
    }

    private static async Task<IReadOnlyList<ProviderFile>> ReadFilesAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        using var document = await ReadJsonAsync(response, cancellationToken);
        var result = new List<ProviderFile>();
        if (document.RootElement.ValueKind != JsonValueKind.Array)
            return result;
        foreach (var item in document.RootElement.EnumerateArray())
        {
            DateTimeOffset? addedAt = null;
            if (item.TryGetProperty("dateAdded", out var date) && date.TryGetInt64(out var epoch))
                addedAt = DateTimeOffset.FromUnixTimeMilliseconds(epoch);
            result.Add(new ProviderFile(GetString(item, "id"), GetString(item, "name"), GetBoolean(item, "uploadedByUser"), GetLong(item, "size"), addedAt));
        }
        return result;
    }

    private static string OAuth1Header(string method, Uri uri, string consumerKey, string consumerSecret)
    {
        var nonce = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        var oauth = new Dictionary<string, string>
        {
            ["oauth_consumer_key"] = consumerKey,
            ["oauth_nonce"] = nonce,
            ["oauth_signature_method"] = "HMAC-SHA1",
            ["oauth_timestamp"] = timestamp,
            ["oauth_version"] = "1.0"
        };
        var parameters = new List<KeyValuePair<string, string>>();
        foreach (var pair in oauth)
            parameters.Add(new(pair.Key, pair.Value));
        foreach (var pair in ParseQuery(uri.Query))
            parameters.Add(pair);
        var normalized = string.Join("&", parameters.OrderBy(x => Encode(x.Key)).ThenBy(x => Encode(x.Value)).Select(x => Encode(x.Key) + "=" + Encode(x.Value)));
        var baseUri = uri.GetLeftPart(UriPartial.Path);
        var signatureBase = method.ToUpperInvariant() + "&" + Encode(baseUri) + "&" + Encode(normalized);
        var signingKey = Encode(consumerSecret) + "&";
        using var hmac = new HMACSHA1(Encoding.UTF8.GetBytes(signingKey));
        oauth["oauth_signature"] = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(signatureBase)));
        return string.Join(", ", oauth.OrderBy(x => x.Key).Select(x => Encode(x.Key) + "=\"" + Encode(x.Value) + "\""));
    }

    private static IEnumerable<KeyValuePair<string, string>> ParseQuery(string query)
    {
        foreach (var part in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var values = part.Split('=', 2);
            yield return new KeyValuePair<string, string>(Uri.UnescapeDataString(values[0]), values.Length > 1 ? Uri.UnescapeDataString(values[1]) : string.Empty);
        }
    }

    private static string Encode(string value) => Uri.EscapeDataString(value).Replace("%20", "%20", StringComparison.Ordinal);
    private static bool TryDecodeProfileCode(string value, out string decoded)
    {
        decoded = string.Empty;
        try
        {
            decoded = Encoding.UTF8.GetString(Convert.FromBase64String(value));
            return decoded.Contains("VIAFIRMA", StringComparison.OrdinalIgnoreCase);
        }
        catch (FormatException)
        {
            return false;
        }
    }
    private static string GetString(JsonElement element, string property) => element.TryGetProperty(property, out var value) && value.ValueKind != JsonValueKind.Null ? value.ToString() : string.Empty;
    private static int GetInt(JsonElement element, string property) => element.TryGetProperty(property, out var value) && value.TryGetInt32(out var result) ? result : 0;
    private static long GetLong(JsonElement element, string property) => element.TryGetProperty(property, out var value) && value.TryGetInt64(out var result) ? result : 0;
    private static int? GetNullableInt(JsonElement element, string property) => element.TryGetProperty(property, out var value) && value.TryGetInt32(out var result) ? result : null;
    private static bool GetBoolean(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var value))
            return false;

        return value.ValueKind == JsonValueKind.True ||
               (value.ValueKind == JsonValueKind.String && value.GetString()?.Equals("true", StringComparison.OrdinalIgnoreCase) == true);
    }
    private static string MapErrorCode(HttpStatusCode statusCode) => statusCode switch { HttpStatusCode.Unauthorized => "provider_unauthorized", HttpStatusCode.Forbidden => "provider_forbidden", HttpStatusCode.NotFound => "provider_not_found", (HttpStatusCode)429 => "provider_rate_limited", _ when (int)statusCode >= 500 => "provider_unavailable", _ => "provider_request_failed" };
}

public sealed class CertificateProviderException : Exception
{
    public CertificateProviderException(string code, string message, int? httpStatus = null, string? providerBody = null) : base(message)
    {
        Code = code;
        HttpStatus = httpStatus;
        ProviderBody = providerBody;
    }

    public string Code { get; }
    public int? HttpStatus { get; }
    public string? ProviderBody { get; }
}
