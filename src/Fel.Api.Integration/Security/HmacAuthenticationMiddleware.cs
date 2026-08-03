using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Fel.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Fel.Core.Entities;

namespace Fel.Api.Security
{
    public class HmacAuthenticationMiddleware
    {
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

            if (!context.Request.Headers.TryGetValue("x-api-key", out var extractedApiKey) ||
                !context.Request.Headers.TryGetValue("x-api-timestamp", out var extractedTimestamp) ||
                !context.Request.Headers.TryGetValue("x-api-signature", out var extractedSignature))
            {
                context.Response.StatusCode = 401;
                await context.Response.WriteAsync("Faltan headers de autenticación HMAC (x-api-key, x-api-timestamp, x-api-signature).");
                return;
            }

            // 1. Prevención de Replay Attacks (Ataques de repetición)
            if (!long.TryParse(extractedTimestamp, out long requestTimestamp))
            {
                context.Response.StatusCode = 400;
                await context.Response.WriteAsync("El x-api-timestamp es inválido.");
                return;
            }

            var requestTime = DateTimeOffset.FromUnixTimeSeconds(requestTimestamp);
            if (Math.Abs((DateTimeOffset.UtcNow - requestTime).TotalMinutes) > 5)
            {
                context.Response.StatusCode = 401;
                await context.Response.WriteAsync("El timestamp ha expirado. (Replay Attack Protection)");
                return;
            }

            // 2. Cargar el API Secret conectando a la base de datos (EF Core) y determinar el entorno
            var apiKeyStr = extractedApiKey.ToString();
            bool isSandbox = apiKeyStr.StartsWith("test_");
            
            Client client = null;
            if (isSandbox)
            {
                client = await dbContext.Clients.FirstOrDefaultAsync(c => c.TestApiKey == apiKeyStr && c.IsActive);
            }
            else
            {
                client = await dbContext.Clients.FirstOrDefaultAsync(c => c.LiveApiKey == apiKeyStr && c.IsActive);
            }
            
            if (client == null)
            {
                context.Response.StatusCode = 401;
                await context.Response.WriteAsync("API Key no válida o el Cliente está inactivo.");
                return;
            }
            
            string clientSecret = isSandbox ? client.TestApiSecret : client.LiveApiSecret;

            // 3. Leer el cuerpo de la petición JSON
            context.Request.EnableBuffering();
            string bodyContent = string.Empty;
            using (var reader = new StreamReader(context.Request.Body, Encoding.UTF8, true, 1024, true))
            {
                bodyContent = await reader.ReadToEndAsync();
            }
            context.Request.Body.Position = 0; // Resetear para que los controladores puedan leerlo

            // 4. Calcular el Hash del Payload
            // Payload estandarizado = Timestamp + "." + JSON Body
            string payloadToSign = $"{extractedTimestamp}.{bodyContent}";
            string computedSignature = ComputeHmacSha256(payloadToSign, clientSecret);

            // 5. Comparar firmas evitando vulnerabilidades de 'Timing Attacks'
            if (!CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(computedSignature), 
                Encoding.UTF8.GetBytes(extractedSignature.ToString())))
            {
                _logger.LogWarning("Acceso denegado: Fallo de integridad HMAC desde IP {IP}.", context.Connection.RemoteIpAddress);
                context.Response.StatusCode = 401;
                await context.Response.WriteAsync("Fallo de integridad HMAC. La firma no coincide (El payload fue alterado o el secret es incorrecto).");
                return;
            }

            // 6. Autorizado: Guardar info del cliente y del entorno en el contexto de la request
            context.Items["ClientId"] = client.Id.ToString();
            context.Items["TenantId"] = client.TenantId.ToString();
            context.Items["IsSandbox"] = isSandbox;

            await _next(context);
        }

        private string ComputeHmacSha256(string payload, string secret)
        {
            var encoding = new System.Text.UTF8Encoding();
            byte[] keyByte = encoding.GetBytes(secret);
            byte[] messageBytes = encoding.GetBytes(payload);

            using (var hmacsha256 = new System.Security.Cryptography.HMACSHA256(keyByte))
            {
                byte[] hashMessage = hmacsha256.ComputeHash(messageBytes);
                return System.Convert.ToBase64String(hashMessage);
            }
        }
    }
}
