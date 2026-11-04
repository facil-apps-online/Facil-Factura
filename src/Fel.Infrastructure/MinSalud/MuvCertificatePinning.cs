using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Fel.Infrastructure.MinSalud
{
    // Confianza en el certificado del proxy del MUV (Mecanismo Único de Validación del Ministerio de Salud). El contenedor corre en el VPS de Bogotá,
    // detrás de un proxy con un certificado autofirmado propio (CN=fevrips-api) al que se llega por el túnel WireGuard. En vez de aceptar cualquier
    // certificado, se confía únicamente en el que tenga la huella SHA-256 configurada (MinSalud:MuvCertificateSha256). Sin huella configurada solo
    // se acepta un certificado válido de la manera normal, así que un certificado autofirmado falla: no se abre la puerta por omisión.
    public static class MuvCertificatePinning
    {
        // "09:21:95:..." o "092195..." (mayúsculas o minúsculas) -> "092195..." en mayúsculas, sin separadores.
        public static string Normalize(string? fingerprint) =>
            new string((fingerprint ?? string.Empty).Where(Uri.IsHexDigit).ToArray()).ToUpperInvariant();

        // Varias huellas separadas por coma, punto y coma o espacio (para poder rotar el certificado sin cortar el servicio). Las que no
        // tengan los 64 caracteres hexadecimales de un SHA-256 se ignoran.
        public static HashSet<string> ParsePins(string? configured) =>
            (configured ?? string.Empty)
                .Split(new[] { ',', ';', ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(Normalize)
                .Where(p => p.Length == 64)
                .ToHashSet(StringComparer.Ordinal);

        // Con huellas configuradas, el certificado del servidor debe coincidir con alguna, sin importar si la cadena es válida.
        // Sin huellas, solo se acepta si el sistema lo considera válido.
        public static bool IsTrusted(X509Certificate? certificate, System.Net.Security.SslPolicyErrors errors, ISet<string> pins)
        {
            if (pins.Count == 0) return errors == System.Net.Security.SslPolicyErrors.None;
            if (certificate == null) return false;

            using var certificate2 = new X509Certificate2(certificate);
            var hash = Convert.ToHexString(certificate2.GetCertHash(HashAlgorithmName.SHA256));
            return pins.Contains(hash);
        }
    }
}
