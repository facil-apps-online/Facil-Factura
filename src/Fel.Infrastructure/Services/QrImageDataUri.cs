using QRCoder;

namespace Fel.Infrastructure.Services;

/// <summary>
/// Genera el QR como PNG en memoria para incrustarlo directamente en los datos
/// enviados al motor de reportes. Evita archivos temporales y servicios externos.
/// </summary>
public static class QrImageDataUri
{
    public static string FromText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(text, QRCodeGenerator.ECCLevel.L);
        var qr = new PngByteQRCode(data);
        var png = qr.GetGraphic(20, drawQuietZones: true);

        return $"data:image/png;base64,{Convert.ToBase64String(png)}";
    }
}
