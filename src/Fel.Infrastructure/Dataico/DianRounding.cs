namespace Fel.Infrastructure.Dataico
{
    // La regla vive en Fel.Core (Fel.Core.Models.DianRounding) para que la compartan el XML nativo, Dataico y el ajuste de centavos;
    // esta clase se conserva para los mapeadores de Dataico y de los reportes que ya la llaman.
    public static class DianRounding
    {
        public static decimal Round2(decimal value) => Fel.Core.Models.DianRounding.Round2(value);
    }
}
