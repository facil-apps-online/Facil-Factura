using System;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading.Tasks;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;

namespace Fel.Infrastructure.Services
{
    // Registra "Software propio" ante el ambiente de habilitación de la DIAN
    // (catalogo-vpfe-hab.dian.gov.co) usando el enlace mágico del representante legal, y lee de
    // vuelta los datos reales que la DIAN asigna para el set de pruebas — nunca se inventan de
    // nuestro lado: TestSetId, prefijo, llave técnica, rango de numeración y las cantidades de
    // documentos requeridas/aceptadas, todo confirmado navegando el portal real (ver
    // /Contributor/ConfigureOperationModes/{id} y /TestSet/View).
    public class DianHabilitationScraperService
    {
        // La DIAN tiene dos portales separados, con enlaces mágicos distintos:
        //   habilitación -> https://catalogo-vpfe-hab.dian.gov.co
        //   producción   -> https://catalogo-vpfe.dian.gov.co
        // El ambiente NO se fija por configuración: se deduce del propio enlace mágico, que ya trae
        // el host del portal al que pertenece. Así el mismo scraper sirve para ambos y es imposible
        // mandar por error un enlace de producción al portal de pruebas o al revés.
        private const string HabilitacionHost = "catalogo-vpfe-hab.dian.gov.co";
        private const string ProduccionHost = "catalogo-vpfe.dian.gov.co";

        public static bool EsEnlaceDeProduccion(string magicLink) =>
            Uri.TryCreate(magicLink, UriKind.Absolute, out var uri) &&
            uri.Host.Equals(ProduccionHost, StringComparison.OrdinalIgnoreCase);

        public static bool EsEnlaceValido(string magicLink) =>
            Uri.TryCreate(magicLink, UriKind.Absolute, out var uri) &&
            (uri.Host.Equals(HabilitacionHost, StringComparison.OrdinalIgnoreCase) ||
             uri.Host.Equals(ProduccionHost, StringComparison.OrdinalIgnoreCase)) &&
            magicLink.Contains("token=", StringComparison.OrdinalIgnoreCase);

        private static string ResolverBaseUrl(string magicLink) =>
            Uri.TryCreate(magicLink, UriKind.Absolute, out var uri)
                ? $"{uri.Scheme}://{uri.Host}"
                : $"https://{HabilitacionHost}";
        private readonly HttpClient _httpClient;
        private readonly ILogger<DianHabilitationScraperService> _logger;

        public DianHabilitationScraperService(HttpClient httpClient, ILogger<DianHabilitationScraperService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            _httpClient.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8");
            _httpClient.DefaultRequestHeaders.Add("Accept-Language", "es-CO,es;q=0.9,en-US;q=0.8,en;q=0.7");
        }

        public async Task<DianHabilitationResult> RegisterSoftwareAndReadTestSetAsync(string magicLink, string softwareName, string? knownSoftwareId = null)
        {
            var result = new DianHabilitationResult();
            var baseUrl = ResolverBaseUrl(magicLink);

            try
            {
                // 1. Canjear el enlace mágico por la sesión autenticada.
                var loginResponse = await _httpClient.GetAsync(magicLink);
                var loginHtml = await loginResponse.Content.ReadAsStringAsync();
                if (!loginResponse.IsSuccessStatusCode)
                {
                    result.ErrorMessage = $"Error al acceder al portal de habilitación de la DIAN: {loginResponse.StatusCode}";
                    return result;
                }

                if (!loginHtml.Contains("Cerrar sesión", StringComparison.OrdinalIgnoreCase))
                {
                    result.ErrorMessage = "El enlace mágico expiró o no es válido. Solicita uno nuevo en el portal de habilitación.";
                    return result;
                }

                // 2. Navegar Registro y habilitación > Documentos electrónicos > Factura electrónica,
                // igual que el flujo real del menú, hasta llegar a la ficha del contribuyente. Con
                // pausas entre pasos — un usuario real tarda segundos entre clics, y hacer las 4-5
                // peticiones en milisegundos parece haber estado disparando algún control de la DIAN
                // que responde con un error genérico de su aplicación en vez de la página real.
                await HumanDelayAsync();
                await _httpClient.GetAsync($"{baseUrl}/Contributor/CheckContributorRegister");
                await HumanDelayAsync();
                var checkResponse = await _httpClient.GetAsync($"{baseUrl}/Contributor/Check");
                var contributorId = ExtractLastPathSegment(checkResponse.RequestMessage?.RequestUri);
                if (string.IsNullOrEmpty(contributorId))
                {
                    result.ErrorMessage = "No se pudo determinar el contribuyente en el portal de la DIAN.";
                    return result;
                }

                _logger.LogInformation("[Habilitación DIAN] Contribuyente resuelto: {ContributorId}", contributorId);

                // 3. Ir a "Configurar modos de operación" — ahí vive tanto el formulario de
                // asociación (form#add-operation-mode-form) como el listado de modos ya asociados.
                var configUrl = $"{baseUrl}/Contributor/ConfigureOperationModes/{contributorId}";
                await HumanDelayAsync();
                var configHtml = await (await _httpClient.GetAsync(configUrl)).Content.ReadAsStringAsync();
                var configDoc = new HtmlDocument();
                configDoc.LoadHtml(configHtml);

                var form = configDoc.GetElementbyId("add-operation-mode-form");
                if (form == null)
                {
                    result.ErrorMessage = "No se encontró el formulario de modos de operación en el portal de la DIAN.";
                    return result;
                }

                // 4. Si "Software propio" ya está asociado (ej. quedó "En proceso" de un intento
                // anterior), reusarlo en vez de intentar asociarlo de nuevo — la DIAN no permite una
                // segunda asociación del mismo modo mientras la primera siga pendiente.
                var testSetViewUrl = FindExistingSoftwarePropioTestSetUrl(configDoc, baseUrl, knownSoftwareId);
                _logger.LogInformation("[Habilitación DIAN] Fila 'Software propio' en el listado ya asociado: {Found}", testSetViewUrl != null);

                if (testSetViewUrl == null)
                {
                    var postResult = await AssociateSoftwarePropioAsync(form, contributorId, softwareName, baseUrl);
                    if (!postResult.IsSuccess)
                    {
                        result.ErrorMessage = postResult.ErrorMessage;
                        return result;
                    }
                    result.SoftwarePin = postResult.SoftwarePin;

                    // Releer el listado ya con la fila nueva para sacar el link real a su set de pruebas.
                    await HumanDelayAsync();
                    var configHtml2 = await (await _httpClient.GetAsync(configUrl)).Content.ReadAsStringAsync();
                    var configDoc2 = new HtmlDocument();
                    configDoc2.LoadHtml(configHtml2);
                    testSetViewUrl = FindExistingSoftwarePropioTestSetUrl(configDoc2, baseUrl, porId: null, porNombre: softwareName);
                    _logger.LogInformation("[Habilitación DIAN] Asociación recién creada, fila releída: {Found}", testSetViewUrl != null);

                    if (testSetViewUrl == null)
                    {
                        result.ErrorMessage = "El software propio se asoció, pero no se encontró su set de pruebas en el portal.";
                        return result;
                    }
                }

                // 5. Leer los datos reales del set de pruebas — nunca inventados de nuestro lado.
                // Con Referer a la página de "Configurar modos de operación": un clic real del ícono
                // siempre lo manda, y sin él la DIAN devolvió antes un error genérico de su propia
                // aplicación ("¡Lo Sentimos! No se pudo procesar la solicitud") en vez de la página.
                await HumanDelayAsync();
                using var testSetRequest = new HttpRequestMessage(HttpMethod.Get, testSetViewUrl);
                testSetRequest.Headers.Referrer = new Uri(configUrl);
                var testSetHtml = await (await _httpClient.SendAsync(testSetRequest)).Content.ReadAsStringAsync();
                var testSetDoc = new HtmlDocument();
                testSetDoc.LoadHtml(testSetHtml);

                _logger.LogInformation("[Habilitación DIAN] GET {Url} -> {Length} bytes de HTML", testSetViewUrl, testSetHtml.Length);

                string GetInputValue(string id)
                {
                    var node = testSetDoc.GetElementbyId(id);
                    if (node == null)
                    {
                        _logger.LogWarning("[Habilitación DIAN] No existe el elemento id={Id} en /TestSet/View", id);
                        return "";
                    }
                    return node.GetAttributeValue("value", "") ?? "";
                }

                result.TestSetId = GetInputValue("TestSetId");
                result.SoftwareId = GetInputValue("SoftwareId");
                if (string.IsNullOrEmpty(result.SoftwarePin)) result.SoftwarePin = GetInputValue("SoftwarePin");
                result.Prefix = GetInputValue("RangePrefix");
                result.ResolutionNumber = GetInputValue("RangeResolutionNumber");
                result.TechnicalKey = GetInputValue("RangeTechnicalKey");
                result.RangeFromNumber = ParseLongOrZero(GetInputValue("RangeFromNumber"));
                result.RangeToNumber = ParseLongOrZero(GetInputValue("RangeToNumber"));
                result.ValidFrom = ParseDianDateOrDefault(GetInputValue("RangeFromDate"));
                result.ValidTo = ParseDianDateOrDefault(GetInputValue("RangeToDate"));

                result.RequiredInvoices = ParseIntOrZero(GetInputValue("InvoicesTotalRequired"));
                result.RequiredDebitNotes = ParseIntOrZero(GetInputValue("TotalDebitNotesRequired"));
                result.RequiredCreditNotes = ParseIntOrZero(GetInputValue("TotalCreditNotesRequired"));
                result.RequiredAcceptedInvoices = ParseIntOrZero(GetInputValue("TotalInvoicesAcceptedRequired"));
                result.RequiredAcceptedDebitNotes = ParseIntOrZero(GetInputValue("TotalDebitNotesAcceptedRequired"));
                result.RequiredAcceptedCreditNotes = ParseIntOrZero(GetInputValue("TotalCreditNotesAcceptedRequired"));

                _logger.LogInformation(
                    "[Habilitación DIAN] Leído — TestSetId='{TestSetId}' SoftwareId='{SoftwareId}' TechnicalKey='{TechnicalKey}' Prefix='{Prefix}' Required={ReqInv}/{ReqDn}/{ReqCn}",
                    result.TestSetId, result.SoftwareId, result.TechnicalKey, result.Prefix,
                    result.RequiredInvoices, result.RequiredDebitNotes, result.RequiredCreditNotes);

                if (string.IsNullOrEmpty(result.TestSetId) || string.IsNullOrEmpty(result.TechnicalKey))
                {
                    // Volcado del HTML crudo (recortado) para diagnosticar qué página devolvió
                    // realmente la DIAN cuando ninguno de los campos esperados apareció — nunca se
                    // había visto esto más allá del snippet, así que no se sabe todavía si es una
                    // redirección a login, un error, u otra causa.
                    var snippet = testSetHtml.Length > 6000 ? testSetHtml.Substring(0, 6000) : testSetHtml;
                    _logger.LogWarning("[Habilitación DIAN] HTML crudo de /TestSet/View (recortado a 6000 chars): {Snippet}", snippet);

                    result.ErrorMessage = "No se pudieron leer los datos del set de pruebas (TestSetId o llave técnica vacíos).";
                    return result;
                }

                result.IsSuccess = true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error crítico registrando/leyendo el set de pruebas de la DIAN");
                result.ErrorMessage = "Error de comunicación con los servidores de la DIAN.";
            }

            return result;
        }

        // Busca, dentro del listado de "modos de operación asociados", la fila cuya primera columna
        // sea exactamente "Software propio" y devuelve la URL absoluta de su enlace a
        // /TestSet/View?... (el ícono "Detalles de set de pruebas") — nunca se arma esa URL a mano
        // porque sus parámetros (operationModeId, contributorCode, softwareId) no son adivinables.
        // Lee el estado real del cliente en los dos portales de la DIAN, sin modificar nada.
        //
        // Se canjean ambos enlaces mágicos en el mismo HttpClient: los portales son hosts distintos
        // (catalogo-vpfe-hab y catalogo-vpfe), así que el CookieContainer mantiene las dos sesiones
        // separadas sin pisarse.
        //
        // Al ser solo lectura, es seguro ejecutarlo siempre antes de tocar nada: de acá sale el plan
        // de qué pasos faltan de verdad, en vez de reintentar a ciegas los que ya están hechos.
        public async Task<DianDiagnostico> DiagnosticarAsync(
            string enlaceHabilitacion,
            string enlaceProduccion,
            string? softwareIdConocido = null)
        {
            var diag = new DianDiagnostico();

            if (!EsEnlaceValido(enlaceHabilitacion) || EsEnlaceDeProduccion(enlaceHabilitacion))
            {
                diag.ErrorMessage = "El primer enlace debe ser del portal de HABILITACIÓN (catalogo-vpfe-hab.dian.gov.co).";
                return diag;
            }
            if (!EsEnlaceValido(enlaceProduccion) || !EsEnlaceDeProduccion(enlaceProduccion))
            {
                diag.ErrorMessage = "El segundo enlace debe ser del portal de PRODUCCIÓN (catalogo-vpfe.dian.gov.co).";
                return diag;
            }

            var urlHab = ResolverBaseUrl(enlaceHabilitacion);
            var urlProd = ResolverBaseUrl(enlaceProduccion);

            try
            {
                // ---------- Habilitación ----------
                var htmlLoginHab = await (await _httpClient.GetAsync(enlaceHabilitacion)).Content.ReadAsStringAsync();
                if (!htmlLoginHab.Contains("Cerrar sesión", StringComparison.OrdinalIgnoreCase))
                {
                    diag.ErrorMessage = "El enlace de habilitación expiró o no es válido.";
                    return diag;
                }

                await HumanDelayAsync();
                var fichaResponse = await _httpClient.GetAsync($"{urlHab}/Contributor/Check");
                diag.ContributorId = ExtractLastPathSegment(fichaResponse.RequestMessage?.RequestUri) ?? "";
                if (string.IsNullOrEmpty(diag.ContributorId))
                {
                    diag.ErrorMessage = "No se pudo determinar el contribuyente en el portal de habilitación.";
                    return diag;
                }

                var fichaDoc = new HtmlDocument();
                fichaDoc.LoadHtml(await fichaResponse.Content.ReadAsStringAsync());

                string Valor(HtmlDocument d, string id) =>
                    HtmlEntity.DeEntitize(d.GetElementbyId(id)?.GetAttributeValue("value", "") ?? "").Trim();

                diag.Nit = Valor(fichaDoc, "Code");
                diag.RazonSocial = Valor(fichaDoc, "BusinessName");
                diag.EstadoAprobacion = Valor(fichaDoc, "AcceptanceStatusName");
                diag.FechaInicioProduccion = Valor(fichaDoc, "ProductionDate");

                await HumanDelayAsync();
                var modosHtml = await (await _httpClient.GetAsync($"{urlHab}/Contributor/ConfigureOperationModes/{diag.ContributorId}")).Content.ReadAsStringAsync();
                var modosDoc = new HtmlDocument();
                modosDoc.LoadHtml(modosHtml);
                diag.ModosEnHabilitacion = LeerModosDeOperacion(modosDoc);

                diag.NuestroSoftware = string.IsNullOrWhiteSpace(softwareIdConocido)
                    ? null
                    : diag.ModosEnHabilitacion.FirstOrDefault(m =>
                        m.SoftwareId.Equals(softwareIdConocido, StringComparison.OrdinalIgnoreCase));

                // ---------- Producción ----------
                var htmlLoginProd = await (await _httpClient.GetAsync(enlaceProduccion)).Content.ReadAsStringAsync();
                if (!htmlLoginProd.Contains("Cerrar sesión", StringComparison.OrdinalIgnoreCase))
                {
                    diag.ErrorMessage = "El enlace de producción expiró o no es válido.";
                    return diag;
                }

                await HumanDelayAsync();
                var fichaProd = await _httpClient.GetAsync($"{urlProd}/Contributor/Check");
                var contributorIdProd = ExtractLastPathSegment(fichaProd.RequestMessage?.RequestUri);
                if (!string.IsNullOrEmpty(contributorIdProd))
                {
                    await HumanDelayAsync();
                    var modosProdHtml = await (await _httpClient.GetAsync($"{urlProd}/Contributor/OperationModes/{contributorIdProd}")).Content.ReadAsStringAsync();
                    var modosProdDoc = new HtmlDocument();
                    modosProdDoc.LoadHtml(modosProdHtml);
                    diag.ModosEnProduccion = LeerModosDeOperacion(modosProdDoc, conEstado: false);
                }

                await HumanDelayAsync();
                var prefijosPagina = await CargarPaginaDePrefijosAsync(urlProd);
                if (prefijosPagina != null)
                    diag.PrefijosAsociados = LeerPrefijosAsociados(prefijosPagina.Doc);

                diag.IsSuccess = true;
                _logger.LogInformation(
                    "[DIAN] Diagnóstico de {Nit}: aprobación={Estado}, producción desde={FechaProd}, modos hab={ModosHab}, modos prod={ModosProd}, prefijos={Prefijos}.",
                    diag.Nit, diag.EstadoAprobacion, diag.FechaInicioProduccion,
                    diag.ModosEnHabilitacion.Count, diag.ModosEnProduccion.Count, diag.PrefijosAsociados.Count);
                return diag;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[DIAN] Error ejecutando el diagnóstico.");
                diag.ErrorMessage = $"Error consultando los portales de la DIAN: {ex.Message}";
                return diag;
            }
        }

        // Habilitación:  Modo | Registro | Estado | Software | Id | Pin | URL | Rangos | Acciones
        // Producción:    Modo | Registro |        | Software | Id | Pin | URL
        // (en producción no hay columna Estado, por eso el desfase de índices)
        private static List<ModoDeOperacion> LeerModosDeOperacion(HtmlDocument doc, bool conEstado = true)
        {
            var modos = new List<ModoDeOperacion>();
            var filas = doc.DocumentNode.SelectNodes("//table//tbody/tr");
            if (filas == null) return modos;

            foreach (var fila in filas)
            {
                var celdas = fila.SelectNodes("./td");
                if (celdas == null || celdas.Count < 5) continue;

                string Celda(int i) => i < celdas.Count ? HtmlEntity.DeEntitize(celdas[i].InnerText).Trim() : string.Empty;

                var modo = Celda(0);
                if (!modo.StartsWith("Software", StringComparison.OrdinalIgnoreCase)) continue;

                modos.Add(new ModoDeOperacion
                {
                    Modo = modo,
                    FechaRegistro = Celda(1),
                    Estado = conEstado ? Celda(2) : string.Empty,
                    NombreSoftware = Celda(conEstado ? 3 : 2),
                    SoftwareId = Celda(conEstado ? 4 : 3),
                    Pin = Celda(conEstado ? 5 : 4)
                });
            }
            return modos;
        }

        // Tabla de /Software/AddNumberRange. El RowKey se toma del data-rk del botón de eliminar,
        // que es la clave con la que el propio portal identifica la fila.
        private static List<PrefijoAsociado> LeerPrefijosAsociados(HtmlDocument doc)
        {
            var prefijos = new List<PrefijoAsociado>();
            var filas = doc.DocumentNode.SelectNodes("//table//tbody/tr");
            if (filas == null) return prefijos;

            foreach (var fila in filas)
            {
                var celdas = fila.SelectNodes("./td");
                if (celdas == null || celdas.Count < 7) continue;

                string Celda(int i) => i < celdas.Count ? HtmlEntity.DeEntitize(celdas[i].InnerText).Trim() : string.Empty;

                prefijos.Add(new PrefijoAsociado
                {
                    Proveedor = Celda(0),
                    NombreSoftware = Celda(1),
                    SoftwareId = Celda(2),
                    TipoDocumento = Celda(3),
                    PrefijoYResolucion = Celda(4),
                    FechaAsociacion = Celda(5),
                    FechaExpiracion = Celda(6),
                    RowKey = fila.SelectSingleNode(".//*[@data-rk]")?.GetAttributeValue("data-rk", "") ?? string.Empty
                });
            }
            return prefijos;
        }

        // Sincroniza el contribuyente al ambiente de producción. Es un paso EXPLÍCITO: el software
        // aprobado en habilitación no aparece en producción hasta que se ejecuta.
        //
        // Vive en la ficha del contribuyente del portal de HABILITACIÓN (/Contributor/View/{id}),
        // botón "Sincronizar Contribuyente a producción" — no en la página de modos de operación, y
        // tampoco en el portal de producción (ahí los modos son de solo lectura).
        //   POST /Contributor/SyncToProduction -> id, __RequestVerificationToken
        //
        // Es idempotente del lado de la DIAN: si el contribuyente ya estaba sincronizado, la ficha
        // muestra una "Fecha de inicio producción" y volver a ejecutarlo no rompe nada.
        public async Task<DianHabilitationResult> SincronizarContribuyenteAProduccionAsync(string magicLink)
        {
            var result = new DianHabilitationResult();
            var baseUrl = ResolverBaseUrl(magicLink);

            if (EsEnlaceDeProduccion(magicLink))
            {
                result.ErrorMessage = "La sincronización se ejecuta desde el portal de habilitación, no desde el de producción. Usa el enlace mágico de habilitación.";
                return result;
            }

            try
            {
                var loginHtml = await (await _httpClient.GetAsync(magicLink)).Content.ReadAsStringAsync();
                if (!loginHtml.Contains("Cerrar sesión", StringComparison.OrdinalIgnoreCase))
                {
                    result.ErrorMessage = "El enlace mágico expiró o no es válido. Solicita uno nuevo en el portal de habilitación.";
                    return result;
                }

                await HumanDelayAsync();
                var checkResponse = await _httpClient.GetAsync($"{baseUrl}/Contributor/Check");
                var contributorId = ExtractLastPathSegment(checkResponse.RequestMessage?.RequestUri);
                if (string.IsNullOrEmpty(contributorId))
                {
                    result.ErrorMessage = "No se pudo determinar el contribuyente en el portal de la DIAN.";
                    return result;
                }

                var fichaHtml = await checkResponse.Content.ReadAsStringAsync();
                var ficha = new HtmlDocument();
                ficha.LoadHtml(fichaHtml);
                var token = ficha.DocumentNode
                    .SelectSingleNode("//input[@name='__RequestVerificationToken']")?
                    .GetAttributeValue("value", "") ?? "";

                if (string.IsNullOrEmpty(token))
                {
                    result.ErrorMessage = "No se encontró el token de la ficha del contribuyente para sincronizar.";
                    return result;
                }

                await HumanDelayAsync();
                var ok = await PostAlPortalAsync($"{baseUrl}/Contributor/SyncToProduction", new Dictionary<string, string>
                {
                    ["id"] = contributorId,
                    ["__RequestVerificationToken"] = token
                });

                if (!ok)
                {
                    result.ErrorMessage = "La DIAN rechazó la sincronización del contribuyente a producción.";
                    return result;
                }

                result.IsSuccess = true;
                _logger.LogInformation("[DIAN] Contribuyente {ContributorId} sincronizado a producción.", contributorId);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[DIAN] Error sincronizando el contribuyente a producción.");
                result.ErrorMessage = $"Error comunicándose con el portal de la DIAN: {ex.Message}";
                return result;
            }
        }

        // Asocia un rango de numeración (prefijo + resolución) al software del cliente en el portal
        // de la DIAN, desasociándolo antes de otro software si hiciera falta.
        //
        // Contrato confirmado navegando el portal real de producción (/Software/AddNumberRange):
        //   POST /Software/RemoveRangeAssociation  -> partitionKey, rowKey, __RequestVerificationToken
        //   POST /Software/AddPrefix               -> softwareId, rangeRowKey, __RequestVerificationToken
        // donde rowKey/rangeRowKey son "{prefijo}|{tipoDocumento}|{numeroResolucion}" — el mismo
        // formato que el portal pone en el data-rk del botón de eliminar.
        //
        // OJO: desasociar un prefijo deja al software anterior SIN poder facturar con él. Es una
        // acción sobre producción real, así que el llamador debe haberla confirmado explícitamente;
        // este método no decide por su cuenta (desasociarDeOtroSoftware viene en false por defecto).
        public async Task<DianPrefixAssociationResult> AsociarPrefijoAlSoftwareAsync(
            string magicLink,
            string softwareId,
            string prefijo,
            string numeroResolucion,
            string tipoDocumento = "01",
            bool desasociarDeOtroSoftware = false)
        {
            var result = new DianPrefixAssociationResult();
            var baseUrl = ResolverBaseUrl(magicLink);
            var rowKey = $"{prefijo}|{tipoDocumento}|{numeroResolucion}";

            try
            {
                var loginResponse = await _httpClient.GetAsync(magicLink);
                var loginHtml = await loginResponse.Content.ReadAsStringAsync();
                if (!loginResponse.IsSuccessStatusCode || !loginHtml.Contains("Cerrar sesión", StringComparison.OrdinalIgnoreCase))
                {
                    result.ErrorMessage = "El enlace mágico expiró o no es válido. Solicita uno nuevo en el portal de la DIAN.";
                    return result;
                }

                await HumanDelayAsync();
                var pagina = await CargarPaginaDePrefijosAsync(baseUrl);
                if (pagina == null)
                {
                    result.ErrorMessage = "No se pudo abrir la página de asociación de prefijos en el portal de la DIAN.";
                    return result;
                }

                // ¿El prefijo ya está asociado a algún software?
                var filaExistente = pagina.Doc.DocumentNode
                    .SelectNodes("//*[@data-rk]")?
                    .FirstOrDefault(n => n.GetAttributeValue("data-rk", "") == rowKey);

                if (filaExistente != null)
                {
                    var celdas = filaExistente.Ancestors("tr").FirstOrDefault()?
                        .SelectNodes("td")?.Select(td => HtmlEntity.DeEntitize(td.InnerText).Trim()).ToList();

                    // Columnas: Proveedor | Software | Código del Software | Tipo | Prefijo | ...
                    result.SoftwareAnterior = celdas != null && celdas.Count > 1 ? celdas[1] : string.Empty;
                    result.CodigoSoftwareAnterior = celdas != null && celdas.Count > 2 ? celdas[2] : string.Empty;

                    if (string.Equals(result.CodigoSoftwareAnterior, softwareId, StringComparison.OrdinalIgnoreCase))
                    {
                        result.IsSuccess = true;
                        result.YaEstabaAsociado = true;
                        _logger.LogInformation("[DIAN] El prefijo {RowKey} ya estaba asociado al software {SoftwareId}; no hay nada que hacer.", rowKey, softwareId);
                        return result;
                    }

                    if (!desasociarDeOtroSoftware)
                    {
                        result.ErrorMessage = $"El prefijo {prefijo} ya está asociado al software \"{result.SoftwareAnterior}\". Para migrarlo hay que desasociarlo primero, y eso dejaría a ese software sin poder facturar con este rango.";
                        return result;
                    }

                    var quitado = await PostAlPortalAsync($"{baseUrl}/Software/RemoveRangeAssociation", new Dictionary<string, string>
                    {
                        ["partitionKey"] = pagina.ContributorCode,
                        ["rowKey"] = rowKey,
                        ["__RequestVerificationToken"] = pagina.Token
                    });

                    if (!quitado)
                    {
                        result.ErrorMessage = $"No se pudo desasociar el prefijo {prefijo} del software \"{result.SoftwareAnterior}\".";
                        return result;
                    }

                    result.RequirioDesasociar = true;
                    _logger.LogInformation("[DIAN] Prefijo {RowKey} desasociado de {SoftwareAnterior}.", rowKey, result.SoftwareAnterior);

                    // Token nuevo: el anterior ya se consumió en el POST anterior.
                    await HumanDelayAsync();
                    pagina = await CargarPaginaDePrefijosAsync(baseUrl);
                    if (pagina == null)
                    {
                        result.ErrorMessage = "Se desasoció el prefijo pero no se pudo recargar la página para asociarlo al nuevo software. Revisa el portal de la DIAN.";
                        return result;
                    }
                }

                var asociado = await PostAlPortalAsync($"{baseUrl}/Software/AddPrefix", new Dictionary<string, string>
                {
                    ["softwareId"] = softwareId,
                    ["rangeRowKey"] = rowKey,
                    ["__RequestVerificationToken"] = pagina.Token
                });

                if (!asociado)
                {
                    result.ErrorMessage = $"No se pudo asociar el prefijo {prefijo} al software {softwareId}.";
                    return result;
                }

                // Verificar contra el portal en vez de confiar en el código de respuesta.
                await HumanDelayAsync();
                var verificacion = await CargarPaginaDePrefijosAsync(baseUrl);
                var quedoAsociado = verificacion?.Doc.DocumentNode
                    .SelectNodes("//*[@data-rk]")?
                    .Any(n => n.GetAttributeValue("data-rk", "") == rowKey) ?? false;

                if (!quedoAsociado)
                {
                    result.ErrorMessage = "La DIAN aceptó la petición pero el prefijo no aparece asociado al recargar. Revisa el portal.";
                    return result;
                }

                result.IsSuccess = true;
                _logger.LogInformation("[DIAN] Prefijo {RowKey} asociado al software {SoftwareId}.", rowKey, softwareId);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[DIAN] Error asociando el prefijo {RowKey} al software {SoftwareId}.", rowKey, softwareId);
                result.ErrorMessage = $"Error comunicándose con el portal de la DIAN: {ex.Message}";
                return result;
            }
        }

        private sealed record PaginaDePrefijos(HtmlDocument Doc, string Token, string ContributorCode);

        private async Task<PaginaDePrefijos?> CargarPaginaDePrefijosAsync(string baseUrl)
        {
            var html = await (await _httpClient.GetAsync($"{baseUrl}/Software/AddNumberRange")).Content.ReadAsStringAsync();
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var token = doc.DocumentNode
                .SelectSingleNode("//input[@name='__RequestVerificationToken']")?
                .GetAttributeValue("value", "") ?? "";
            var contributorCode = doc.DocumentNode
                .SelectSingleNode("//input[@id='ContributorCode']")?
                .GetAttributeValue("value", "") ?? "";

            if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(contributorCode))
            {
                _logger.LogWarning("[DIAN] La página de prefijos no trajo token ({TokenVacio}) o contribuyente ({ContribVacio}).",
                    string.IsNullOrEmpty(token), string.IsNullOrEmpty(contributorCode));
                return null;
            }

            return new PaginaDePrefijos(doc, token, contributorCode);
        }

        private async Task<bool> PostAlPortalAsync(string url, Dictionary<string, string> campos)
        {
            var respuesta = await _httpClient.PostAsync(url, new FormUrlEncodedContent(campos));
            var cuerpo = await respuesta.Content.ReadAsStringAsync();
            if (!respuesta.IsSuccessStatusCode)
            {
                _logger.LogWarning("[DIAN] POST {Url} respondió {Codigo}.", url, respuesta.StatusCode);
                return false;
            }
            // El portal responde 200 incluso en error de negocio; se descarta el caso obvio.
            if (cuerpo.Contains("\"success\":false", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("[DIAN] POST {Url} respondió success:false.", url);
                return false;
            }
            return true;
        }

        // Busca la fila de NUESTRO software propio, identificada por el SoftwareId que la DIAN nos
        // asignó y que tenemos guardado.
        //
        // No basta con que el modo de operación diga "Software propio": un contribuyente puede tener
        // ya uno registrado que no es el nuestro (desarrollado por él o por otro proveedor). Adoptar
        // la primera fila que diga "Software propio" significaría tomar SU TestSetId, SoftwareId y
        // PIN, y ese SoftwareID viaja dentro del XML de cada factura — estaríamos emitiendo bajo un
        // registro de software ajeno.
        //
        // Se compara por ID y no por nombre porque el nombre lo componemos nosotros ("FF - {razón
        // social}") y puede cambiar, mientras que el ID lo asigna la DIAN y es inmutable.
        //
        // knownSoftwareId vacío = todavía no hemos registrado nada para este cliente, así que no hay
        // ninguna fila que nos pertenezca y hay que crear una nueva. (Si un registro anterior sí
        // llegó a la DIAN pero no alcanzamos a guardar el ID, el portal rechazará el duplicado: es
        // un fallo visible, preferible a adoptar en silencio el software equivocado.)
        // Se busca por ID cuando ya conocemos el nuestro (caso normal), y por nombre solo
        // inmediatamente después de haberlo registrado nosotros mismos — ahí todavía no tenemos el
        // ID (es justo lo que vamos a leer), pero la fila recién creada es nuestra por construcción.
        // Nunca se busca "la primera fila que diga Software propio" sin más.
        private static string? FindExistingSoftwarePropioTestSetUrl(HtmlDocument doc, string baseUrl, string? porId, string? porNombre = null)
        {
            if (string.IsNullOrWhiteSpace(porId) && string.IsNullOrWhiteSpace(porNombre)) return null;

            var rows = doc.DocumentNode.SelectNodes("//table//tbody/tr");
            if (rows == null) return null;

            foreach (var row in rows)
            {
                var cells = row.SelectNodes("./td");
                if (cells == null || cells.Count == 0) continue;

                var firstCellText = HtmlEntity.DeEntitize(cells[0].InnerText).Trim();
                if (!firstCellText.Equals("Software propio", StringComparison.OrdinalIgnoreCase)) continue;

                // Columnas del listado de modos de operación: 3 = "Software", 4 = "Id".
                if (!string.IsNullOrWhiteSpace(porId))
                {
                    var idEnFila = cells.Count > 4 ? HtmlEntity.DeEntitize(cells[4].InnerText).Trim() : string.Empty;
                    if (!idEnFila.Equals(porId, StringComparison.OrdinalIgnoreCase)) continue;
                }
                else
                {
                    var nombreEnFila = cells.Count > 3 ? HtmlEntity.DeEntitize(cells[3].InnerText).Trim() : string.Empty;
                    if (!nombreEnFila.Equals(porNombre, StringComparison.OrdinalIgnoreCase)) continue;
                }

                var link = row.SelectSingleNode(".//a[contains(@href, '/TestSet/View')]");
                // El HTML trae el href con entidades escapadas ("...?a=1&amp;b=2"), igual que el
                // texto de la celda — sin desescapar, "&amp;" queda literal en la URL y la DIAN solo
                // recibe el primer parámetro de la query string como válido (el resto llega pegado
                // a "amp;"), lo que le hace devolver un error genérico de su aplicación.
                var href = HtmlEntity.DeEntitize(link?.GetAttributeValue("href", null));
                if (string.IsNullOrEmpty(href)) continue;

                return href.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? href : $"{baseUrl}{href}";
            }

            return null;
        }

        private async Task<(bool IsSuccess, string ErrorMessage, string SoftwarePin)> AssociateSoftwarePropioAsync(HtmlNode form, string contributorId, string softwareName, string baseUrl)
        {
            string GetFieldValue(string name) => form.SelectSingleNode($".//*[@name='{name}']")?.GetAttributeValue("value", "") ?? "";

            // "Software propio" no siempre está disponible en el <select> (ej. si ya hay uno
            // asociado, la DIAN lo retira de las opciones) — si no aparece, no hay nada que asociar.
            var operationModeSelect = form.SelectSingleNode(".//select[@id='OperationModeId']");
            var softwarePropioOption = operationModeSelect?.SelectSingleNode(".//option[normalize-space(text())='Software propio']");
            var operationModeValue = softwarePropioOption?.GetAttributeValue("value", null);
            if (string.IsNullOrEmpty(operationModeValue))
            {
                return (false, "\"Software propio\" no está disponible para asociar en este momento (es posible que ya haya una asociación pendiente).", string.Empty);
            }

            var softwarePin = GenerateRandomPin();
            var softwareIdValue = GetFieldValue("Software.Id");
            var softwareUrlValue = GetFieldValue("Software.Url");
            var token = GetFieldValue("__RequestVerificationToken");

            var formData = new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<string, string>>
            {
                new("__RequestVerificationToken", token),
                new("Id", GetFieldValue("Id")),
                new("Code", GetFieldValue("Code")),
                new("ContributorTypeId", GetFieldValue("ContributorTypeId")),
                new("OperationModeId", operationModeValue),
                new("Software.Id", softwareIdValue),
                new("Software.Name", softwareName),
                new("Software.Pin", softwarePin),
                new("Software.Url", softwareUrlValue),
            };

            var response = await _httpClient.PostAsync($"{baseUrl}/Contributor/AddContributorOperations", new FormUrlEncodedContent(formData));
            if (!response.IsSuccessStatusCode)
            {
                return (false, $"La DIAN rechazó la asociación del software propio: {response.StatusCode}", string.Empty);
            }

            return (true, string.Empty, softwarePin);
        }

        private static string GenerateRandomPin() => RandomNumberGenerator.GetInt32(10000, 100000).ToString(CultureInfo.InvariantCulture);

        // Pausa entre 1.5 y 3 segundos, variable a propósito (no fija) para no dejar un patrón de
        // tiempos idéntico entre peticiones — simula el ritmo de navegación de una persona real.
        private static Task HumanDelayAsync() =>
            Task.Delay(TimeSpan.FromMilliseconds(RandomNumberGenerator.GetInt32(1500, 3000)));

        private static string? ExtractLastPathSegment(Uri? uri)
        {
            if (uri == null) return null;
            var segments = uri.Segments;
            return segments.Length == 0 ? null : segments[^1].Trim('/');
        }

        private static long ParseLongOrZero(string value) => long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result) ? result : 0;

        private static int ParseIntOrZero(string value) => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result) ? result : 0;

        // Fechas del portal en formato dd-MM-yyyy (ej. "19-01-2019").
        private static DateTime ParseDianDateOrDefault(string value) =>
            DateTime.TryParseExact(value, "dd-MM-yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var result)
                ? result
                : default;
    }

    public class DianHabilitationResult
    {
        public bool IsSuccess { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;

        public string TestSetId { get; set; } = string.Empty;
        public string SoftwareId { get; set; } = string.Empty;
        public string SoftwarePin { get; set; } = string.Empty;

        public string Prefix { get; set; } = string.Empty;
        public string ResolutionNumber { get; set; } = string.Empty;
        public string TechnicalKey { get; set; } = string.Empty;
        public long RangeFromNumber { get; set; }
        public long RangeToNumber { get; set; }
        public DateTime ValidFrom { get; set; }
        public DateTime ValidTo { get; set; }

        public int RequiredInvoices { get; set; }
        public int RequiredCreditNotes { get; set; }
        public int RequiredDebitNotes { get; set; }
        public int RequiredAcceptedInvoices { get; set; }
        public int RequiredAcceptedCreditNotes { get; set; }
        public int RequiredAcceptedDebitNotes { get; set; }
    }

    public class DianPrefixAssociationResult
    {
        public bool IsSuccess { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;

        /// <summary>Software al que estaba asociado el prefijo antes de migrarlo, si lo había.</summary>
        public string SoftwareAnterior { get; set; } = string.Empty;
        public string CodigoSoftwareAnterior { get; set; } = string.Empty;

        /// <summary>true si hubo que desasociarlo de otro software para poder migrarlo.</summary>
        public bool RequirioDesasociar { get; set; }

        /// <summary>true si el prefijo ya estaba asociado a nuestro software (nada que hacer).</summary>
        public bool YaEstabaAsociado { get; set; }
    }
}
