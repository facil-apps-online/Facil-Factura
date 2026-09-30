using System.Linq;

namespace Fel.Core.Security
{
    /// <summary>
    /// Política de contraseñas para los cuatro portales (cliente, tenant, superadmin, developers).
    /// </summary>
    /// <remarks>
    /// Antes de esto, la única barrera para una contraseña nueva era <c>minLength={8}</c> en el
    /// HTML de cada <c>ResetPassword.tsx</c> — nada la exigía en el servidor, así que una petición
    /// directa a la API (sin pasar por el formulario) podía dejar una contraseña de un solo
    /// carácter. Esta clase es la que de verdad decide si una contraseña se acepta; el medidor de
    /// fortaleza del frontend (<c>apps/_shared/components/PasswordStrength.tsx</c>) solo la
    /// refleja para que el usuario no llegue a enviarla y se la rechacen.
    ///
    /// Regla: 8 caracteres o más, y al menos 3 de estos 4 grupos: mayúsculas, minúsculas,
    /// números, símbolos.
    /// </remarks>
    public static class PasswordPolicy
    {
        public const int MinLength = 8;
        public const int MinCharacterClasses = 3;

        public static bool IsValid(string? password) => Validate(password) == null;

        /// <summary>Null si la contraseña cumple la política; si no, el mensaje para mostrar.</summary>
        public static string? Validate(string? password)
        {
            if (string.IsNullOrEmpty(password))
            {
                return "La contraseña es obligatoria.";
            }

            if (password.Length < MinLength)
            {
                return $"La contraseña debe tener al menos {MinLength} caracteres.";
            }

            var classes = CountCharacterClasses(password);
            if (classes < MinCharacterClasses)
            {
                return $"La contraseña debe combinar al menos {MinCharacterClasses} de estos: mayúsculas, minúsculas, números y símbolos.";
            }

            return null;
        }

        private static int CountCharacterClasses(string password)
        {
            bool hasLower = password.Any(char.IsLower);
            bool hasUpper = password.Any(char.IsUpper);
            bool hasDigit = password.Any(char.IsDigit);
            bool hasSymbol = password.Any(c => !char.IsLetterOrDigit(c));

            return new[] { hasLower, hasUpper, hasDigit, hasSymbol }.Count(x => x);
        }
    }
}
