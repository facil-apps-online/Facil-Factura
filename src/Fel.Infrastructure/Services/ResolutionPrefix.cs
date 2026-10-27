using System.Linq;

namespace Fel.Infrastructure.Services
{
    // El prefijo de una resolución de la DIAN se guarda siempre en mayúsculas y sin espacios: así "jl" y "JL" son el mismo y coincide con lo que
    // devuelve Dataico, aunque la pantalla muestre el campo en mayúsculas sin importar lo digitado.
    public static class ResolutionPrefix
    {
        public static string Normalize(string? prefix) =>
            new string((prefix ?? string.Empty).Where(c => !char.IsWhiteSpace(c)).ToArray()).ToUpperInvariant();
    }
}
