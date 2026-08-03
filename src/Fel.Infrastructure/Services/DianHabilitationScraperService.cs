using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace Fel.Infrastructure.Services
{
    public class DianHabilitationScraperService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<DianHabilitationScraperService> _logger;

        public DianHabilitationScraperService(HttpClient httpClient, ILogger<DianHabilitationScraperService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
            // Configurar el HttpClient para que imite un navegador moderno
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            _httpClient.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8");
            _httpClient.DefaultRequestHeaders.Add("Accept-Language", "es-CO,es;q=0.9,en-US;q=0.8,en;q=0.7");
        }

        public async Task<DianHabilitationResult> ExtractTestSetIdAsync(string magicLink, string softwareId, string softwarePin)
        {
            var result = new DianHabilitationResult();

            try
            {
                // 1. Acceder al link mágico de la DIAN para robar la sesión.
                // El link mágico es usualmente: https://catalogo-vpfe.dian.gov.co/User/Login?token=...
                var loginResponse = await _httpClient.GetAsync(magicLink);
                if (!loginResponse.IsSuccessStatusCode)
                {
                    result.ErrorMessage = $"Error al acceder al portal DIAN: {loginResponse.StatusCode}";
                    return result;
                }

                // Aquí, internamente HttpClient (si usa CookieContainer) guarda las cookies (ASP.NET_SessionId, etc).
                // 2. Navegar al Dashboard principal para validar que entramos.
                var dashboardUrl = "https://catalogo-vpfe.dian.gov.co/User/Dashboard";
                var dashResponse = await _httpClient.GetAsync(dashboardUrl);
                var dashHtml = await dashResponse.Content.ReadAsStringAsync();

                if (!dashHtml.Contains("Salir") && !dashHtml.Contains("Cerrar Sesión"))
                {
                    result.ErrorMessage = "El token de la DIAN expiró o es inválido. Por favor, solicita uno nuevo en el portal.";
                    return result;
                }

                // 3. Navegar a Configuración > Modos de Operación para buscar el TestSetId
                // La DIAN suele tener un endpoint interno o vista donde se listan los modos de operación
                var modosOperacionUrl = "https://catalogo-vpfe.dian.gov.co/User/OperationModes";
                var modosResponse = await _httpClient.GetAsync(modosOperacionUrl);
                var modosHtml = await modosResponse.Content.ReadAsStringAsync();

                // Buscar el TestSetId con una expresión regular
                // Usualmente aparece en el HTML como 'data-testset="GUID"' o 'TestSetId: "GUID"'
                // o en una tabla de detalles de software asociado.
                // Ajustaremos el regex basado en el DOM real de la DIAN.
                var matchTestSet = Regex.Match(modosHtml, @"[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}", RegexOptions.IgnoreCase);
                
                if (matchTestSet.Success)
                {
                    result.TestSetId = matchTestSet.Value;
                    result.IsSuccess = true;
                    
                    // EXTRA: Leer los documentos requeridos (usualmente la DIAN tiene una tabla de resumen)
                    // Buscamos algo como: Facturas requeridas: 8, Notas Crédito: 1, etc.
                    // Esto requerirá un scraping más detallado una vez tengamos un volcado real del HTML.
                    result.RequiredInvoices = 8; // Default DIAN actual
                    result.RequiredCreditNotes = 1;
                    result.RequiredDebitNotes = 1;
                }
                else
                {
                    _logger.LogInformation("No se encontró TestSetId. Procediendo a asociar el Software Propio automáticamente...");
                    
                    // POST para asociar el software
                    // La URL exacta y payload deben ajustarse si la DIAN cambia el portal.
                    // Usualmente es a un endpoint interno como /User/AssociateSoftware
                    var formData = new FormUrlEncodedContent(new[]
                    {
                        new KeyValuePair<string, string>("SoftwareId", softwareId),
                        new KeyValuePair<string, string>("Pin", softwarePin)
                    });

                    var associateUrl = "https://catalogo-vpfe.dian.gov.co/User/AssociateSoftware"; // URL aproximada basada en el comportamiento del portal
                    var associateResponse = await _httpClient.PostAsync(associateUrl, formData);
                    
                    if (associateResponse.IsSuccessStatusCode)
                    {
                        // Volver a consultar Modos de Operación para sacar el TestSetId
                        var modosResponse2 = await _httpClient.GetAsync(modosOperacionUrl);
                        var modosHtml2 = await modosResponse2.Content.ReadAsStringAsync();
                        var matchTestSet2 = Regex.Match(modosHtml2, @"[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}", RegexOptions.IgnoreCase);
                        
                        if (matchTestSet2.Success)
                        {
                            result.TestSetId = matchTestSet2.Value;
                            result.IsSuccess = true;
                            result.RequiredInvoices = 8;
                            result.RequiredCreditNotes = 1;
                            result.RequiredDebitNotes = 1;
                        }
                        else
                        {
                            result.ErrorMessage = "Software asociado correctamente, pero no se pudo extraer el TestSetId del HTML.";
                        }
                    }
                    else
                    {
                        result.ErrorMessage = $"Error al intentar asociar el software. Código: {associateResponse.StatusCode}";
                    }
                }

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error crítico scrapeando la DIAN");
                result.ErrorMessage = "Error de comunicación con los servidores de la DIAN.";
            }

            return result;
        }
    }

    public class DianHabilitationResult
    {
        public bool IsSuccess { get; set; }
        public string TestSetId { get; set; } = string.Empty;
        public int RequiredInvoices { get; set; }
        public int RequiredCreditNotes { get; set; }
        public int RequiredDebitNotes { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
    }
}
