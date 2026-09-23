using System.Collections.Generic;

namespace Fel.Core.Entities
{
    /// <summary>
    /// Catálogos del LoginSISPRO del MUV-FEV-RIPS, según el "Manual de Consumo API-DOCKER-FEV-RIPS".
    /// </summary>
    /// <remarks>
    /// Viven acá y se sirven por endpoint en vez de escribirse dentro de los <c>&lt;select&gt;</c>
    /// de cada portal: así agregar un valor se hace en un solo archivo y no quedan portales
    /// mostrando listas distintas del mismo catálogo.
    ///
    /// Ojo con la distinción, que el modelo anterior tenía fundida en un solo campo y hacía fallar
    /// la autenticación: el <b>tipo de documento</b> va en <c>persona.identificacion.tipo</c> y el
    /// <b>tipo de usuario</b> en <c>tipoUsuario</c>. Son catálogos distintos.
    /// </remarks>
    public static class MinSaludCatalogs
    {
        public record OpcionCatalogo(string Codigo, string Nombre);

        /// <summary>Tipo de documento de quien autentica (persona.identificacion.tipo).</summary>
        public static readonly IReadOnlyList<OpcionCatalogo> TiposDeDocumento = new List<OpcionCatalogo>
        {
            new("CC", "Cédula de ciudadanía"),
            new("CE", "Cédula de extranjería"),
            new("PA", "Pasaporte"),
            new("NIT", "NIT"),
            new("PE", "Permiso especial de permanencia"),
            new("PT", "Permiso por protección temporal")
        };

        /// <summary>
        /// Campo tipoUsuario del LoginSISPRO. Es opcional; el manual indica que para PSS y PTS,
        /// cuando se informa, debe ser RE.
        /// </summary>
        public static readonly IReadOnlyList<OpcionCatalogo> TiposDeUsuario = new List<OpcionCatalogo>
        {
            new("RE", "Representante de entidad"),
            new("PIN", "Profesional independiente nacional"),
            new("PINx", "Profesional independiente nacional de excepción"),
            new("PIE", "Profesional independiente extranjero")
        };

        /// <summary>Ambientes del MUV. Coinciden con <see cref="MinSaludEnvironments"/>.</summary>
        public static readonly IReadOnlyList<OpcionCatalogo> Ambientes = new List<OpcionCatalogo>
        {
            new(MinSaludEnvironments.Test, "Pruebas"),
            new(MinSaludEnvironments.Production, "Producción")
        };
    }
}
