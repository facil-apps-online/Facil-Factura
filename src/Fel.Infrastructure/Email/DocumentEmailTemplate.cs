using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using Fel.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Fel.Infrastructure.Email
{
    // Datos de un correo de documento: quién lo emite, qué documento es y el color de fondo ya resuelto.
    public record DocumentEmailModel(
        string IssuerName, string? IssuerTaxId, string? IssuerAddress, string? IssuerPhone, string? IssuerEmail, string? LogoUrl,
        string DocumentLabel, string Number, DateTime IssueDate, decimal Total, string Currency, string BackgroundColor);

    // Colores del correo de documentos. Cada nivel (cliente, tenant, plataforma) puede guardar un color claro y uno oscuro; el primer nivel que
    // tenga alguno manda, y de su pareja se usa el que más contraste tenga con la tarjeta blanca. Sin ninguno, el azul por defecto.
    public static class DocumentEmailColors
    {
        public const string Default = "#2563eb";
        private static readonly Regex Hex = new("^#[0-9a-fA-F]{6}$", RegexOptions.Compiled);

        // El color en minúsculas si es #rrggbb, o null (vacío o inválido = heredar).
        public static string? Normalize(string? color)
        {
            var trimmed = color?.Trim();
            return trimmed != null && Hex.IsMatch(trimmed) ? trimmed.ToLowerInvariant() : null;
        }

        public static string Resolve(params (string? Light, string? Dark)[] levels)
        {
            foreach (var (light, dark) in levels)
            {
                var l = Normalize(light);
                var d = Normalize(dark);
                if (l != null && d != null) return ContrastWithWhite(d) > ContrastWithWhite(l) ? d : l;
                if (l != null || d != null) return (l ?? d)!;
            }
            return Default;
        }

        public static async Task<(string?, string?)> TenantColorsAsync(FelDbContext db, Guid tenantId)
        {
            var t = await db.Tenants.AsNoTracking().Where(x => x.Id == tenantId).Select(x => new { x.EmailColorLight, x.EmailColorDark }).FirstOrDefaultAsync();
            return (t?.EmailColorLight, t?.EmailColorDark);
        }

        public static async Task<(string?, string?)> PlatformColorsAsync(FelDbContext db)
        {
            var p = await db.PlatformEmailSettings.AsNoTracking().Select(x => new { x.EmailColorLight, x.EmailColorDark }).FirstOrDefaultAsync();
            return (p?.EmailColorLight, p?.EmailColorDark);
        }

        // Color del texto que va directamente sobre el fondo (el que está fuera de la tarjeta).
        public static string TextOn(string background) => ContrastWithWhite(background) >= 3 ? "#e2e8f0" : "#1e293b";

        public static double ContrastWithWhite(string hex) => 1.05 / (RelativeLuminance(hex) + 0.05);

        private static double RelativeLuminance(string hex)
        {
            double Channel(int offset)
            {
                var c = Convert.ToInt32(hex.Substring(offset, 2), 16) / 255.0;
                return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
            }
            return 0.2126 * Channel(1) + 0.7152 * Channel(3) + 0.0722 * Channel(5);
        }
    }

    // El cuerpo del correo con el que se entrega un documento electrónico al adquirente. El asunto y el adjunto los fija la DIAN (Anexo Técnico,
    // numeral 9.1): lo único que cambia entre emisores es la identidad (logo, razón social y color de fondo). Todo el HTML es en línea y con
    // tablas porque los clientes de correo no entienden CSS externo.
    public static class DocumentEmailTemplate
    {
        public static string Build(DocumentEmailModel m)
        {
            string E(string? s) => WebUtility.HtmlEncode(s ?? string.Empty);
            var background = DocumentEmailColors.Normalize(m.BackgroundColor) ?? DocumentEmailColors.Default;
            var outside = DocumentEmailColors.TextOn(background);

            var logo = string.IsNullOrWhiteSpace(m.LogoUrl)
                ? string.Empty
                : $@"<td style=""padding-right:14px""><img src=""{E(m.LogoUrl)}"" alt="""" height=""44"" style=""display:block;height:44px;max-width:160px""></td>";

            var issuerLine = string.IsNullOrWhiteSpace(m.IssuerTaxId) ? E(m.IssuerName) : $"{E(m.IssuerName)} · NIT {E(m.IssuerTaxId)}";
            var contactLine = string.Join(" · ", new[] { m.IssuerAddress, m.IssuerPhone, m.IssuerEmail }.Where(s => !string.IsNullOrWhiteSpace(s)).Select(E));

            var html = $@"<!DOCTYPE html>
<html lang=""es""><head><meta charset=""utf-8""><meta name=""viewport"" content=""width=device-width,initial-scale=1""><title>{E(m.DocumentLabel)} {E(m.Number)}</title></head>
<body style=""margin:0;padding:0;background:{background};font-family:Arial,Helvetica,sans-serif;color:#1e293b"">
<table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""background:{background};padding:32px 12px""><tr><td align=""center"">
<table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""max-width:560px;background:#ffffff;border-radius:12px"">
<tr><td style=""padding:28px 32px 8px 32px""><table role=""presentation"" cellpadding=""0"" cellspacing=""0""><tr>{logo}<td style=""font-size:17px;font-weight:bold;color:#0f172a"">{E(m.IssuerName)}</td></tr></table></td></tr>
<tr><td style=""padding:20px 32px 8px 32px;font-size:15px;line-height:1.7;color:#334155"">
<p style=""margin:0 0 14px 0"">Hola,</p>
<p style=""margin:0 0 14px 0"">{E(m.IssuerName)} te envía su {E(m.DocumentLabel)} <strong>{E(m.Number)}</strong>, emitida el {m.IssueDate:dd/MM/yyyy} por un total de <strong>{E(FormatTotal(m.Total, m.Currency))}</strong>.</p>
<p style=""margin:0 0 14px 0"">Adjunto encontrarás un archivo ZIP con el XML del documento, que trae la validación de la DIAN, y su representación gráfica en PDF. Guarda el ZIP: el XML es el documento válido.</p>
</td></tr>
<tr><td style=""padding:12px 32px 28px 32px;font-size:12px;line-height:1.6;color:#64748b;border-top:1px solid #e2e8f0"">{issuerLine}{(contactLine.Length > 0 ? "<br>" + contactLine : string.Empty)}</td></tr>
</table>
<p style=""margin:16px 0 0 0;font-size:11px;color:{outside}"">Enviado con FacilFactura</p>
</td></tr></table></body></html>";
            return html;
        }

        private static string FormatTotal(decimal total, string currency)
        {
            // Punto de miles y coma decimal, sin depender de la cultura del servidor.
            var format = new NumberFormatInfo { NumberGroupSeparator = ".", NumberDecimalSeparator = "," };
            if (string.IsNullOrWhiteSpace(currency) || currency == "COP") return "$ " + total.ToString("#,##0.##", format);
            return $"{currency} " + total.ToString("#,##0.00", format);
        }
    }
}
