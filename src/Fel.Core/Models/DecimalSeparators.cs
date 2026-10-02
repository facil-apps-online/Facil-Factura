namespace Fel.Core.Models
{
    // Valores válidos de Client.DecimalSeparator. Punto = "1,234,567.89" (punto decimal, coma de miles; el
    // estándar y el valor por defecto); Coma = "1.234.567,89" (coma decimal, punto de miles).
    public static class DecimalSeparators
    {
        public const string Point = ".";
        public const string Comma = ",";

        public static bool IsValid(string? value) => value == Point || value == Comma;

        // El separador de miles es siempre el otro símbolo.
        public static string GroupFor(string decimalSeparator) => decimalSeparator == Comma ? Point : Comma;
    }
}
