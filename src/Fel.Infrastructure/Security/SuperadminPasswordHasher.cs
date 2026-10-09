using System;
using System.Security.Cryptography;
using System.Text;

namespace Fel.Infrastructure.Security
{
    // Contraseñas del superadmin. Hasta ahora se guardaban con SHA-256 y una sal fija en el código, que es débil; las nuevas se guardan con
    // BCrypt como en el resto de portales. Las anteriores siguen sirviendo y se pasan a BCrypt la primera vez que entran bien, así nadie
    // tiene que cambiar su contraseña por esto.
    public static class SuperadminPasswordHasher
    {
        public static string Hash(string password) => BCrypt.Net.BCrypt.HashPassword(password);

        // BCrypt siempre empieza por "$2".
        public static bool IsLegacy(string hash) => !hash.StartsWith("$2", StringComparison.Ordinal);

        public static bool Verify(string password, string hash) =>
            IsLegacy(hash)
                ? CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(LegacyHash(password)), Encoding.UTF8.GetBytes(hash))
                : BCrypt.Net.BCrypt.Verify(password, hash);

        private static string LegacyHash(string password)
        {
            using var sha256 = SHA256.Create();
            return Convert.ToBase64String(sha256.ComputeHash(Encoding.UTF8.GetBytes(password + "FEL_SALT_SECURE")));
        }
    }
}
