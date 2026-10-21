using System;
using System.Collections.Generic;
using System.Linq;

namespace Fel.Infrastructure.Services
{
    // Reparte el nombre completo de una persona natural en nombre 1, nombre 2, apellido 1 y apellido 2, con la misma regla del formulario de tercero del
    // portal: las partículas DE, DEL, LA, LAS, LOS, Y, SAN y SANTA se pegan a la palabra siguiente ("DEL PILAR", "DE LA CRUZ"); con 2 unidades es nombre y
    // apellido, con 3 es un nombre y dos apellidos, y con 4 o más son dos nombres, un apellido y el resto como segundo apellido. Es una aproximación: quien
    // revisa el borrador puede corregirla.
    public static class ColombianNameSplitter
    {
        private static readonly HashSet<string> Particles = new(StringComparer.OrdinalIgnoreCase) { "DE", "DEL", "LA", "LAS", "LOS", "Y", "SAN", "SANTA" };

        public static (string First, string Second, string FirstLast, string SecondLast) Split(string? fullName)
        {
            var words = (fullName ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var units = new List<string>();
            var current = string.Empty;
            for (var i = 0; i < words.Length; i++)
            {
                current = current.Length == 0 ? words[i] : current + " " + words[i];
                if (Particles.Contains(words[i]) && i < words.Length - 1) continue;
                units.Add(current);
                current = string.Empty;
            }
            if (current.Length > 0) units.Add(current);

            return units.Count switch
            {
                0 => (string.Empty, string.Empty, string.Empty, string.Empty),
                1 => (units[0], string.Empty, string.Empty, string.Empty),
                2 => (units[0], string.Empty, units[1], string.Empty),
                3 => (units[0], string.Empty, units[1], units[2]),
                _ => (units[0], units[1], units[2], string.Join(" ", units.Skip(3)))
            };
        }
    }
}
