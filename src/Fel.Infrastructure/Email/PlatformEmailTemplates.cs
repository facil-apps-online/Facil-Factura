using System.Net;

namespace Fel.Infrastructure.Email
{
    // Correos de plataforma que salen sin cliente: invitación a crear la contraseña y recuperación de contraseña. Llevan la marca del tenant
    // dueño de la cuenta (logo y nombre) porque es la que reconoce quien los recibe.
    public static class PlatformEmailTemplates
    {
        public const string Invitation = "invitation";
        public const string PasswordReset = "password_reset";

        public static (string Subject, string Html) Build(string templateType, string userName, string link, string logoUrl, string brandName, TimeSpan linkLifetime)
        {
            var name = WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(userName) ? "hola" : userName);
            var brand = WebUtility.HtmlEncode(brandName);
            var hours = (int)Math.Round(linkLifetime.TotalHours);
            var validity = hours <= 1 ? "1 hora" : $"{hours} horas";

            var (subject, title, intro, button) = templateType == Invitation
                ? ($"{brandName}: crea tu contraseña", "Te damos la bienvenida", $"Se creó una cuenta para ti en {brand}. Para empezar, crea tu contraseña con el siguiente botón.", "Crear mi contraseña")
                : ($"{brandName}: restablece tu contraseña", "Restablece tu contraseña", $"Recibimos una solicitud para restablecer la contraseña de tu cuenta en {brand}. Si fuiste tú, usa el siguiente botón.", "Restablecer contraseña");

            var safeLink = WebUtility.HtmlEncode(link);
            var html = $@"<!DOCTYPE html>
<html lang=""es""><body style=""margin:0;padding:0;background:#f1f5f9;font-family:Arial,Helvetica,sans-serif;color:#1e293b"">
<table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""background:#f1f5f9;padding:24px 0""><tr><td align=""center"">
<table role=""presentation"" width=""560"" cellpadding=""0"" cellspacing=""0"" style=""max-width:560px;width:100%;background:#ffffff;border-radius:12px"">
<tr><td style=""padding:28px 32px 8px 32px""><img src=""{WebUtility.HtmlEncode(logoUrl)}"" alt=""{brand}"" style=""max-height:48px;max-width:220px""></td></tr>
<tr><td style=""padding:8px 32px 0 32px""><h1 style=""font-size:22px;margin:16px 0 8px 0"">{title}</h1>
<p style=""font-size:15px;line-height:1.5;margin:0 0 12px 0"">Hola {name},</p>
<p style=""font-size:15px;line-height:1.5;margin:0 0 24px 0"">{intro}</p></td></tr>
<tr><td align=""center"" style=""padding:0 32px 24px 32px""><a href=""{safeLink}"" style=""display:inline-block;background:#2563eb;color:#ffffff;text-decoration:none;font-weight:bold;font-size:15px;padding:12px 28px;border-radius:8px"">{button}</a></td></tr>
<tr><td style=""padding:0 32px 8px 32px""><p style=""font-size:13px;line-height:1.5;color:#64748b;margin:0 0 8px 0"">El enlace sirve una sola vez y vence en {validity}. Si el botón no funciona, copia esta dirección en tu navegador:</p>
<p style=""font-size:12px;line-height:1.4;color:#64748b;word-break:break-all;margin:0 0 16px 0"">{safeLink}</p></td></tr>
<tr><td style=""padding:0 32px 28px 32px;border-top:1px solid #e2e8f0""><p style=""font-size:12px;color:#94a3b8;margin:16px 0 0 0"">Si no esperabas este correo, puedes ignorarlo: tu cuenta no cambia hasta que uses el enlace.</p></td></tr>
</table></td></tr></table></body></html>";
            return (subject, html);
        }
    }
}
