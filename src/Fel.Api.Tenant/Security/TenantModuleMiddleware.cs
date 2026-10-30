using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Fel.Infrastructure.Data;
using Fel.Infrastructure.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Fel.Api.Security
{
    // Módulos de la cuenta (ver TenantModules): lo que el tenant no tiene contratado se rechaza con 403 aunque se llame la API directamente; ocultar
    // el menú en el portal no basta.
    //
    // - Sin Facturación electrónica solo queda lo que necesita un tenant de certificados: sesión, apariencia y SMTP, países, archivos públicos,
    //   el alta y la ficha básica de sus clientes (titulares) y sus certificados. Todo lo demás es de facturación y se rechaza (lista blanca:
    //   un endpoint nuevo de facturación queda cerrado por defecto para estos tenants).
    // - Sin Certificados se rechaza la solicitud a Viafirma (perfiles y solicitudes), el saldo prepagado y su configuración. La consulta y la
    //   carga manual del .p12 siguen, porque la emisión directa las necesita para firmar.
    public class TenantModuleMiddleware
    {
        public const string InvoicingMessage = "Tu cuenta no tiene el módulo de facturación electrónica.";
        public const string CertificatesMessage = "Tu cuenta no tiene el módulo de certificados digitales.";

        private const string GuidPattern = "[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}";

        private static readonly string[] CertificatesOnlyPrefixes =
        {
            "/api/tenant/auth/", "/api/tenant/branding", "/api/tenant/smtp-settings", "/api/tenant/countries", "/api/tenant/files/",
            "/api/tenant/certificates"
        };

        // Clientes (titulares): listado, alta, lectura de RUT, ficha básica, archivar/restaurar, bloqueo y su certificado.
        private static readonly Regex CertificatesOnlyClientPaths = new(
            $"^/api/tenant/clients(/parse-rut|/{GuidPattern}(/block|/reactivate|/certificate(/.*)?)?)?/?$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

        // Solicitud a Viafirma (perfiles y solicitudes de un cliente), el listado de solicitudes, el saldo prepagado y la configuración de
        // entrega. La descarga de un certificado ya emitido sigue disponible.
        private static readonly Regex ViafirmaRequestPaths = new(
            $"^/api/tenant/(clients/{GuidPattern}/certificate/(options|requests)|certificates/(requests|balance|settings))(/.*)?$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

        private readonly RequestDelegate _next;

        public TenantModuleMiddleware(RequestDelegate next) => _next = next;

        public async Task InvokeAsync(HttpContext context)
        {
            if (!HttpMethods.IsOptions(context.Request.Method) && Guid.TryParse(context.User?.FindFirst("TenantId")?.Value, out var tenantId))
            {
                var path = context.Request.Path.Value ?? string.Empty;
                var dbContext = context.RequestServices.GetRequiredService<FelDbContext>();
                var modules = await TenantModules.GetAsync(dbContext, tenantId);

                if (!modules.Invoicing && !IsCertificatesOnlyPath(path))
                {
                    context.Response.StatusCode = 403;
                    await context.Response.WriteAsync(InvoicingMessage);
                    return;
                }
                if (!modules.Certificates && ViafirmaRequestPaths.IsMatch(path))
                {
                    context.Response.StatusCode = 403;
                    await context.Response.WriteAsync(CertificatesMessage);
                    return;
                }
            }

            await _next(context);
        }

        private static bool IsCertificatesOnlyPath(string path)
        {
            foreach (var prefix in CertificatesOnlyPrefixes)
                if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return true;
            return CertificatesOnlyClientPaths.IsMatch(path);
        }
    }
}
