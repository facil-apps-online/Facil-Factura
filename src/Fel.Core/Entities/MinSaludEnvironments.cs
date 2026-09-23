namespace Fel.Core.Entities
{
    /// <summary>
    /// Ambientes del Mecanismo Único de Validación (MUV) de FEV-RIPS del Ministerio de Salud.
    /// </summary>
    /// <remarks>
    /// No son dos configuraciones del mismo servicio: el Ministerio publica dos imágenes de Docker
    /// distintas, desde registries distintos, y cada una apunta a un host propio —
    /// <c>fevrips.sispro.gov.co</c> para producción y <c>stage-fevrips.sispropreprod.gov.co</c>
    /// para pruebas. Las credenciales de un ambiente no sirven en el otro.
    /// </remarks>
    public static class MinSaludEnvironments
    {
        /// <summary>Ambiente de pruebas del Ministerio. Es el valor por defecto: un Client nuevo
        /// no debería emitir contra producción hasta que alguien lo decida explícitamente.</summary>
        public const string Test = "Test";

        /// <summary>Ambiente productivo. Lo que se emita acá cuenta como reporte real.</summary>
        public const string Production = "Production";

        public static bool EsValido(string? valor) =>
            valor == Test || valor == Production;
    }
}
