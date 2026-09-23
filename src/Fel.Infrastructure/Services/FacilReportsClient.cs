using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Fel.Core.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Fel.Infrastructure.Services
{
    public class FacilReportsClient : IFacilReportsClient
    {
        private readonly HttpClient _http;
        private readonly ILogger<FacilReportsClient> _logger;
        private readonly string _apiUrl;
        private readonly string _apiKey;

        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };

        public FacilReportsClient(HttpClient http, IConfiguration config, ILogger<FacilReportsClient> logger)
        {
            _http = http;
            _logger = logger;
            _apiUrl = (config["FacilReports:ApiUrl"] ?? config["FacilReports__ApiUrl"] ?? "https://reports.facil-apps.online").TrimEnd('/');
            _apiKey = config["FacilReports:ApiKey"] ?? config["FacilReports__ApiKey"] ?? string.Empty;
        }

        public async Task<byte[]?> GenerateReportAsync(string templateKey, Dictionary<string, object?> data, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(_apiKey))
            {
                _logger.LogWarning("Facil Reports no configurado (falta la API key); no se genera PDF para la plantilla {TemplateKey}.", templateKey);
                return null;
            }

            var payload = new { templateKey, data };
            using var req = new HttpRequestMessage(HttpMethod.Post, $"{_apiUrl}/api/reports/generate")
            {
                Content = new StringContent(JsonSerializer.Serialize(payload, JsonOpts), Encoding.UTF8, "application/json")
            };
            req.Headers.TryAddWithoutValidation("X-API-Key", _apiKey);
            req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/pdf"));

            try
            {
                using var resp = await _http.SendAsync(req, ct);
                if (!resp.IsSuccessStatusCode)
                {
                    var body = await resp.Content.ReadAsStringAsync(ct);
                    _logger.LogError("Facil Reports generate falló ({Code}) para {TemplateKey}: {Body}", (int)resp.StatusCode, templateKey, body);
                    return null;
                }
                return await resp.Content.ReadAsByteArrayAsync(ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Facil Reports generate threw para {TemplateKey}.", templateKey);
                return null;
            }
        }

        public async Task<bool> UploadTemplateAsync(string templateKey, byte[] repxBytes, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(_apiKey))
            {
                _logger.LogWarning("Facil Reports no configurado (falta la API key); no se sube la plantilla {TemplateKey}.", templateKey);
                return false;
            }

            var payload = new { templateKey, repxBase64 = Convert.ToBase64String(repxBytes) };
            using var req = new HttpRequestMessage(HttpMethod.Post, $"{_apiUrl}/api/templates/save")
            {
                Content = new StringContent(JsonSerializer.Serialize(payload, JsonOpts), Encoding.UTF8, "application/json")
            };
            req.Headers.TryAddWithoutValidation("X-API-Key", _apiKey);

            try
            {
                using var resp = await _http.SendAsync(req, ct);
                if (!resp.IsSuccessStatusCode)
                {
                    var body = await resp.Content.ReadAsStringAsync(ct);
                    _logger.LogError("Facil Reports templates/save falló ({Code}) para {TemplateKey}: {Body}", (int)resp.StatusCode, templateKey, body);
                    return false;
                }
                return true;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Facil Reports templates/save threw para {TemplateKey}.", templateKey);
                return false;
            }
        }
    }
}
