using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Fel.Infrastructure.Data;
using Fel.Core.Entities;
using Fel.Infrastructure.Security;

namespace Fel.Api.Security
{
    // Autenticación de la API de integración: la llave (x-api-key) y su secreto (x-api-secret) en los headers, siempre sobre HTTPS.
    // No hay firma del cuerpo ni marca de tiempo: el integrador solo copia las dos credenciales que ve en el portal de developers.
    // El nombre de la clase se conserva por historia (antes firmaba con HMAC).
    public class HmacAuthenticationMiddleware
    {
        public const string ApiKeyHeader = "x-api-key";
        public const string ApiSecretHeader = "x-api-secret";

        private readonly RequestDelegate _next;
        private readonly ILogger<HmacAuthenticationMiddleware> _logger;

        public HmacAuthenticationMiddleware(RequestDelegate next, ILogger<HmacAuthenticationMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context, FelDbContext dbContext)
        {
            // Ignorar peticiones OPTIONS (CORS preflight)
            if (HttpMethods.IsOptions(context.Request.Method))
            {
                await _next(context);
                return;
            }

            // Aplicar solo a los endpoints de la API B2B (Excluir Swagger, Docs, Superadmin y Tenant)
            if (!context.Request.Path.StartsWithSegments("/api") ||
                context.Request.Path.StartsWithSegments("/api/superadmin") ||
                context.Request.Path.StartsWithSegments("/api/tenant"))
            {
                await _next(context);
                return;
            }

            var apiKey = context.Request.Headers[ApiKeyHeader].ToString().Trim();
            var apiSecret = context.Request.Headers[ApiSecretHeader].ToString().Trim();
            if (apiKey.Length == 0 || apiSecret.Length == 0)
            {
                var missing = apiKey.Length == 0 && apiSecret.Length == 0 ? $"{ApiKeyHeader} y {ApiSecretHeader}" : apiKey.Length == 0 ? ApiKeyHeader : ApiSecretHeader;
                await Reject(context, 401, "MISSING_CREDENTIALS", $"Falta el header {missing}. Envía tu API Key en x-api-key y tu API Secret en x-api-secret.");
                return;
            }

            // Las llaves son de la sucursal: de ahí salen el cliente, la sucursal y el entorno (llaves "test_" = sandbox).
            var credentials = await ApiCredentialResolver.ResolveAsync(dbContext, apiKey);
            if (credentials == null)
            {
                await Reject(context, 401, "INVALID_API_KEY", "La API Key no existe, o el cliente o la sucursal están inactivos. Las llaves de pruebas empiezan por test_.");
                return;
            }

            // Comparación en tiempo constante, para que la demora no revele cuánto del secreto coincide.
            if (!CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(credentials.Secret ?? string.Empty), Encoding.UTF8.GetBytes(apiSecret)))
            {
                _logger.LogWarning("Acceso denegado: API Secret incorrecto para la llave {ApiKey} desde IP {IP}.", apiKey, context.Connection.RemoteIpAddress);
                await Reject(context, 401, "INVALID_API_SECRET", "El API Secret no corresponde a esta API Key. Verifica que uses el secreto del mismo ambiente (producción o pruebas).");
                return;
            }

            if (credentials.Block != TenantBlockLevel.None)
            {
                await Reject(context, 403, "ACCOUNT_BLOCKED", TenantBlocks.ClientMessage);
                return;
            }

            // Autorizado: Guardar info del cliente y del entorno en el contexto de la request
            context.Items["ClientId"] = credentials.Client.Id.ToString();
            context.Items["BranchId"] = credentials.Branch.Id.ToString();
            context.Items["TenantId"] = credentials.Client.TenantId.ToString();
            context.Items["IsSandbox"] = credentials.IsSandbox;

            await _next(context);
        }

        private static Task Reject(HttpContext context, int status, string code, string message)
        {
            context.Response.StatusCode = status;
            return context.Response.WriteAsJsonAsync(new { code, message });
        }
    }
}
