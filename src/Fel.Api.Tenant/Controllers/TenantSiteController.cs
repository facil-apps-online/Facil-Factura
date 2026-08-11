using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Fel.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using SkiaSharp;

namespace Fel.Api.Tenant.Controllers
{
    [ApiController]
    [Route("api/tenant/site")]
    public class TenantSiteController : ControllerBase
    {
        private readonly FelDbContext _dbContext;
        private readonly IHttpClientFactory _httpFactory;
        private readonly IMemoryCache _cache;

        public TenantSiteController(FelDbContext dbContext, IHttpClientFactory httpFactory, IMemoryCache cache)
        {
            _dbContext = dbContext;
            _httpFactory = httpFactory;
            _cache = cache;
        }

        private const string BaseUrl = "https://clients.facil-factura.pro";

        // HTML del micrositio público del tenant (para compartir en WhatsApp y landing)
        [HttpGet("{slug}")]
        public async Task<IActionResult> GetSite(string slug)
        {
            var tenant = await _dbContext.Tenants
                .Where(t => t.Slug == slug && t.IsActive)
                .Select(t => new { t.Name, t.CommercialName, t.LogoLightUrl, t.PrimaryColorLight, t.DefaultLanguageCode })
                .FirstOrDefaultAsync();

            if (tenant == null)
            {
                return NotFound(new { Message = "Micrositio no encontrado o inactivo." });
            }

            var color = string.IsNullOrWhiteSpace(tenant.PrimaryColorLight) ? "#0ea5e9" : tenant.PrimaryColorLight;
            var name = HttpUtility.HtmlEncode(tenant.CommercialName);
            var title = $"{name} | Portal de Facturación Electrónica";
            var description = HttpUtility.HtmlEncode($"Portal de facturación electrónica de {tenant.CommercialName}. Emite tus comprobantes, consulta tus documentos y gestiona tu facturación en línea.");
            var ogImage = $"{BaseUrl}/og/{slug}.png";

            var html = $@"<!doctype html>
<html lang=""es"">
  <head>
    <meta charset=""UTF-8"" />
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"" />
    <title>{title}</title>
    <meta name=""description"" content=""{description}"" />
    <link rel=""icon"" type=""image/png"" href=""{ogImage}"" />
    <meta name=""theme-color"" content=""{color}"" />
    <meta property=""og:type"" content=""website"" />
    <meta property=""og:url"" content=""{BaseUrl}/{slug}"" />
    <meta property=""og:title"" content=""{title}"" />
    <meta property=""og:description"" content=""{description}"" />
    <meta property=""og:image"" content=""{ogImage}"" />
    <meta property=""og:image:width"" content=""1200"" />
    <meta property=""og:image:height"" content=""630"" />
    <meta property=""og:locale"" content=""es_CO"" />
    <meta property=""og:site_name"" content=""{name}"" />
    <meta name=""twitter:card"" content=""summary_large_image"" />
    <meta name=""twitter:title"" content=""{title}"" />
    <meta name=""twitter:description"" content=""{description}"" />
    <meta name=""twitter:image"" content=""{ogImage}"" />
    <style>
      * {{ margin: 0; padding: 0; box-sizing: border-box; }}
      body {{ font-family: 'Segoe UI', system-ui, sans-serif; background: #0B1120; color: #f8fafc; min-height: 100vh; display: flex; flex-direction: column; }}
      main {{ flex: 1; display: flex; flex-direction: column; align-items: center; justify-content: center; text-align: center; padding: 2rem; gap: 1.5rem; }}
      .logo {{ max-width: 200px; max-height: 80px; object-fit: contain; }}
      .empty-logo {{ width: 72px; height: 72px; border-radius: 16px; background: {color}; display: flex; align-items: center; justify-content: center; font-size: 28px; font-weight: 800; color: #fff; }}
      h1 {{ font-size: 2rem; font-weight: 800; }}
      p {{ color: #94a3b8; max-width: 480px; line-height: 1.6; }}
      a.btn {{ background: {color}; color: #fff; text-decoration: none; font-weight: 700; padding: 0.9rem 2rem; border-radius: 12px; box-shadow: 0 10px 30px rgba(0,0,0,0.3); }}
      a.btn:hover {{ opacity: 0.9; }}
      footer {{ text-align: center; padding: 1.5rem; font-size: 0.8rem; color: #475569; }}
    </style>
  </head>
  <body>
    <main>
      {LogoHtml(tenant.LogoLightUrl, name)}
      <h1>{name}</h1>
      <p>{description}</p>
      <a class=""btn"" href=""{BaseUrl}/"">Entrar al Portal de Facturación</a>
    </main>
    <footer>Facturación electrónica DIAN · {name}</footer>
  </body>
</html>";

            return Content(html, "text/html; charset=utf-8");
        }

        // Imagen OG 1200x630 generada por tenant (logo sobre color principal)
        [HttpGet("{slug}/og-image.png")]
        public async Task<IActionResult> GetOgImage(string slug, CancellationToken ct)
        {
            var cacheKey = $"og_{slug}";
            if (_cache.TryGetValue(cacheKey, out byte[] cached))
            {
                return File(cached, "image/png");
            }

            var tenant = await _dbContext.Tenants
                .Where(t => t.Slug == slug && t.IsActive)
                .Select(t => new { t.CommercialName, t.LogoLightUrl, t.PrimaryColorLight })
                .FirstOrDefaultAsync(ct);

            if (tenant == null)
            {
                return NotFound();
            }

            var color = string.IsNullOrWhiteSpace(tenant.PrimaryColorLight) ? "#0ea5e9" : tenant.PrimaryColorLight;
            var bytes = await BuildOgImageAsync(tenant.CommercialName, tenant.LogoLightUrl, color, ct);

            _cache.Set(cacheKey, bytes, TimeSpan.FromHours(6));
            return File(bytes, "image/png");
        }

        private string LogoHtml(string logoUrl, string name)
        {
            if (!string.IsNullOrWhiteSpace(logoUrl))
            {
                return $"<img class=\"logo\" src=\"{logoUrl}\" alt=\"{name}\" />";
            }
            var initial = string.IsNullOrWhiteSpace(name) ? "F" : name.Substring(0, 1).ToUpper();
            return $"<div class=\"empty-logo\">{initial}</div>";
        }

        private async Task<byte[]> BuildOgImageAsync(string name, string logoUrl, string colorHex, CancellationToken ct)
        {
            const int width = 1200;
            const int height = 630;

            using var bitmap = new SKBitmap(width, height);
            using var canvas = new SKCanvas(bitmap);

            // Fondo degradado con el color principal
            using var paint = new SKPaint { IsAntialias = true };
            paint.Shader = SKShader.CreateLinearGradient(
                new SKPoint(0, 0), new SKPoint(width, height),
                new[] { ParseColor(colorHex), ParseColor(Darken(colorHex)) },
                null, SKShaderTileMode.Clamp);
            canvas.DrawRect(new SKRect(0, 0, width, height), paint);

            // Logo del tenant (o isotipo inicial)
            SKBitmap? logo = null;
            if (!string.IsNullOrWhiteSpace(logoUrl))
            {
                logo = await LoadRemoteImageAsync(logoUrl, ct);
            }

            if (logo != null)
            {
                int drawW = (int)(width * 0.45);
                int drawH = (int)(logo.Height * (double)drawW / logo.Width);
                if (drawH > height * 0.5) { drawH = (int)(height * 0.5); drawW = (int)(logo.Width * (double)drawH / logo.Height); }
                var dst = new SKRect((width - drawW) / 2, (height - drawH) / 2, (width + drawW) / 2, (height + drawH) / 2);
                canvas.DrawBitmap(logo, dst);
                logo.Dispose();
            }
            else
            {
                using var typeface = SKTypeface.FromFamilyName("Arial");
                using var textFont = new SKFont(typeface, 64) { Embolden = true };
                using var textPaint = new SKPaint
                {
                    IsAntialias = true,
                    Color = SKColors.White
                };
                var initial = string.IsNullOrWhiteSpace(name) ? "F" : name.Substring(0, 1).ToUpper();
                canvas.DrawText(initial, width / 2f, height / 2f + 24, SKTextAlign.Center, textFont, textPaint);
            }

            // Nombre del tenant
            if (!string.IsNullOrWhiteSpace(name))
            {
                using var typeface = SKTypeface.FromFamilyName("Arial");
                using var nameFont = new SKFont(typeface, 44) { Embolden = true };
                using var namePaint = new SKPaint
                {
                    IsAntialias = true,
                    Color = SKColors.White
                };
                canvas.DrawText(name, width / 2f, height - 90, SKTextAlign.Center, nameFont, namePaint);
            }

            using var image = SKImage.FromBitmap(bitmap);
            using var data = image.Encode(SKEncodedImageFormat.Png, 90);
            return data.ToArray();
        }

        private async Task<SKBitmap?> LoadRemoteImageAsync(string url, CancellationToken ct)
        {
            try
            {
                var http = _httpFactory.CreateClient();
                http.Timeout = TimeSpan.FromSeconds(10);
                var bytes = await http.GetByteArrayAsync(url, ct);
                if (bytes.Length == 0) return null;
                using var ms = new MemoryStream(bytes);
                var bmp = SKBitmap.Decode(ms);
                return bmp;
            }
            catch
            {
                return null;
            }
        }

        private static SKColor ParseColor(string hex)
        {
            hex = hex.TrimStart('#');
            if (hex.Length == 6 && byte.TryParse(hex.Substring(0, 2), System.Globalization.NumberStyles.HexNumber, null, out var r)
                && byte.TryParse(hex.Substring(2, 2), System.Globalization.NumberStyles.HexNumber, null, out var g)
                && byte.TryParse(hex.Substring(4, 2), System.Globalization.NumberStyles.HexNumber, null, out var b))
            {
                return new SKColor(r, g, b);
            }
            return new SKColor(14, 165, 233);
        }

        private static string Darken(string hex)
        {
            hex = hex.TrimStart('#');
            if (hex.Length != 6) return "#0369a1";
            var r = Math.Max(0, Convert.ToInt32(hex.Substring(0, 2), 16) - 40);
            var g = Math.Max(0, Convert.ToInt32(hex.Substring(2, 2), 16) - 40);
            var b = Math.Max(0, Convert.ToInt32(hex.Substring(4, 2), 16) - 40);
            return $"#{r:x2}{g:x2}{b:x2}";
        }
    }
}
