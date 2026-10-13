namespace Fel.Core.Models
{
    // Responsabilidades de renta del RUT (casilla 53). Son códigos fijos de la DIAN y excluyentes
    // entre sí: un contribuyente declara renta por una sola de estas vías.
    public static class IncomeTaxRegimes
    {
        public const string Ordinary = "05";
        public const string Special = "04";
        public const string Simple = "47";

        // Vacío o nulo = aún no capturado, y eso es válido.
        public static bool IsValid(string? code) =>
            string.IsNullOrEmpty(code) || code == Ordinary || code == Special || code == Simple;

        // Lo que se imprime en la representación gráfica; vacío cuando no hay régimen capturado.
        public static string Label(string? code) => code switch
        {
            Ordinary => "Régimen Ordinario",
            Special => "Régimen Especial",
            Simple => "Régimen Simple de Tributación",
            _ => string.Empty
        };
    }
}
