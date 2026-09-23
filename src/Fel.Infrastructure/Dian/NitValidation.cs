using System.Linq;

namespace Fel.Infrastructure.Dian
{
    // Compara NITs ignorando formato (guiones, dígito de verificación, espacios) — el NIT que trae
    // el certificado (SERIALNUMBER) y el que tiene registrado el Client rara vez vienen con el
    // mismo formato exacto aunque sean el mismo número.
    public static class NitValidation
    {
        public static bool Matches(string? a, string? b)
        {
            var normalizedA = Normalize(a);
            var normalizedB = Normalize(b);

            if (normalizedA.Length == 0 || normalizedB.Length == 0) return false;

            // Uno de los dos podría traer el dígito de verificación pegado (ej. "9001234567" vs
            // "900123456") — se toleran ambas formas en vez de exigir el mismo largo exacto.
            return normalizedA == normalizedB
                || normalizedA.StartsWith(normalizedB)
                || normalizedB.StartsWith(normalizedA);
        }

        private static string Normalize(string? nit) =>
            nit == null ? string.Empty : new string(nit.Where(char.IsDigit).ToArray());

        // Dígito de verificación (DV) del NIT, algoritmo módulo 11 publicado por la DIAN (mismo que
        // usa el RUT). Antes se mandaba un "1" fijo en el schemeID de cbc:CompanyID — la DIAN
        // rechaza eso con "DV del NIT ... no está correctamente calculado".
        private static readonly int[] Weights = { 3, 7, 13, 17, 19, 23, 29, 37, 41, 43, 47, 53, 59, 67, 71 };

        public static int CalculateCheckDigit(string? nit)
        {
            var digits = Normalize(nit);
            if (digits.Length == 0) return 0;

            long sum = 0;
            for (int i = 0; i < digits.Length && i < Weights.Length; i++)
            {
                var digit = digits[digits.Length - 1 - i] - '0';
                sum += digit * Weights[i];
            }

            var remainder = sum % 11;
            return remainder <= 1 ? (int)remainder : 11 - (int)remainder;
        }
    }
}
