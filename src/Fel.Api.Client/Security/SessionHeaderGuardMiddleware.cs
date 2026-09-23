using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace Fel.Api.Security
{
    // Mismo propósito que su equivalente en Fel.Api.Tenant, pero cubriendo los 2 headers de
    // sesión que vive este proyecto: "x-client-id" (portal de clientes, ClientAuthController) y
    // "x-developer-id" (portal de developers, DeveloperAuthController). Las rutas HMAC (B2B,
    // /api/*) no llevan ninguno de estos 2 headers, así que este middleware no las afecta.
    public class SessionHeaderGuardMiddleware
    {
        private readonly RequestDelegate _next;

        public SessionHeaderGuardMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            if (!CheckHeader(context, "x-client-id", "ClientId") ||
                !CheckHeader(context, "x-developer-id", "DeveloperId"))
            {
                context.Response.StatusCode = 401;
                await context.Response.WriteAsync("Sesión inválida o expirada. Vuelve a iniciar sesión.");
                return;
            }

            await _next(context);
        }

        private static bool CheckHeader(HttpContext context, string headerName, string claimType)
        {
            if (!context.Request.Headers.TryGetValue(headerName, out var headerValue)) return true;

            var claimValue = context.User?.FindFirst(claimType)?.Value;
            return !string.IsNullOrEmpty(claimValue) && string.Equals(claimValue, headerValue.ToString(), StringComparison.OrdinalIgnoreCase);
        }
    }
}
