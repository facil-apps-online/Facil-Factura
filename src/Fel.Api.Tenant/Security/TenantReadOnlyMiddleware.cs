using System;
using System.Threading.Tasks;
using Fel.Infrastructure.Data;
using Fel.Infrastructure.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Fel.Api.Security
{
    // Suspensión de la plataforma (p. ej. por mora): el tenant entra a su portal pero solo puede consultar. Toda petición que modifica
    // (POST, PUT, PATCH, DELETE) se rechaza con 403, salvo la sesión (login, cambio de contraseña, cierre de sesión) y las vistas previas o
    // lecturas de archivos que viajan por POST pero no guardan nada. El estado sale de TenantBlocks, con el tenant del token.
    public class TenantReadOnlyMiddleware
    {
        public const string Message = "Su cuenta está suspendida y está en modo de solo lectura. Comuníquese con su proveedor.";

        private static readonly string[] Allowed = { "/api/tenant/auth/" };
        // Lecturas que usan POST porque envían un archivo o un cuerpo, pero no guardan nada.
        private static readonly string[] ReadOnlyPostSuffixes = { "/parse-rut", "/parse", "/preview" };

        private readonly RequestDelegate _next;

        public TenantReadOnlyMiddleware(RequestDelegate next) => _next = next;

        public async Task InvokeAsync(HttpContext context)
        {
            var method = context.Request.Method;
            var isRead = HttpMethods.IsGet(method) || HttpMethods.IsHead(method) || HttpMethods.IsOptions(method);

            if (!isRead && Guid.TryParse(context.User?.FindFirst("TenantId")?.Value, out var tenantId) && !IsAllowed(context.Request.Path.Value ?? string.Empty))
            {
                var dbContext = context.RequestServices.GetRequiredService<FelDbContext>();
                var block = await TenantBlocks.GetAsync(dbContext, tenantId);
                if (block.Level == TenantBlockLevel.ReadOnly)
                {
                    context.Response.StatusCode = 403;
                    var detail = string.IsNullOrWhiteSpace(block.Reason) ? string.Empty : $" Motivo: {block.Reason}";
                    await context.Response.WriteAsync(Message + detail);
                    return;
                }
            }

            await _next(context);
        }

        private static bool IsAllowed(string path)
        {
            var p = path.ToLowerInvariant();
            foreach (var prefix in Allowed) if (p.StartsWith(prefix)) return true;
            foreach (var suffix in ReadOnlyPostSuffixes) if (p.EndsWith(suffix)) return true;
            return false;
        }
    }
}
