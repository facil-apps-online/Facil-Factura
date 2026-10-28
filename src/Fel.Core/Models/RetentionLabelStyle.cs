namespace Fel.Core.Models
{
    // Cómo se ve cada retención en los selectores de los formularios: el nombre completo del concepto ("Compras generales (declarantes) (RET_FUENTE 2.5%)")
    // o solo el nombre corto con la tarifa ("RETE FUENTE 2.5%"). Se elige por cliente, aparte para las retenciones de cada línea y para las del pie.
    public static class RetentionLabelStyle
    {
        public const string Full = "Full";
        public const string Short = "Short";

        public static bool IsValid(string? value) => value is Full or Short;
    }
}
