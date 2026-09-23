using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace Fel.Api.Security
{
    // El portal de tenants sigue enviando "x-tenant-id" en cada petición (todos los controladores
    // ya lo leen así — ver GetCurrentTenantId en cada uno) pero antes ese header no se verificaba
    // contra nada: cualquiera podía mandar el x-tenant-id de otro tenant y el backend lo aceptaba
    // sin más. Este middleware no cambia esa lectura — solo exige que, cuando el header viene, el
    // JWT de la petición (Authorization: Bearer, emitido por TenantAuthController.Login) traiga la
    // claim "TenantId" con el MISMO valor, o rechaza con 401 antes de llegar al controlador.
    public class SessionHeaderGuardMiddleware
    {
        private readonly RequestDelegate _next;

        public SessionHeaderGuardMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            if (context.Request.Headers.TryGetValue("x-tenant-id", out var headerValue))
            {
                var claimValue = context.User?.FindFirst("TenantId")?.Value;
                if (string.IsNullOrEmpty(claimValue) || !string.Equals(claimValue, headerValue.ToString(), StringComparison.OrdinalIgnoreCase))
                {
                    context.Response.StatusCode = 401;
                    await context.Response.WriteAsync("Sesión inválida o expirada. Vuelve a iniciar sesión.");
                    return;
                }
            }

            await _next(context);
        }
    }
}
