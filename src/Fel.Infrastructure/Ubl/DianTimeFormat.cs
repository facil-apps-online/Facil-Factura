using System;

namespace Fel.Infrastructure.Ubl
{
    // Colombia no tiene horario de verano: UTC-5 fijo todo el año. Los DateTime que llegan a esta
    // capa se generan en UTC (DateTime.UtcNow) en todo el sistema, pero la DIAN exige la hora en
    // zona horaria -5 (Anexo Técnico, regla FAD10 — "Debe ser informada la hora en una zona
    // horaria -5, que es la zona horaria oficial de Colombia"), notificación que salía en cada
    // envío de prueba. Único punto de conversión, usado tanto por el cálculo de CUFE/CUDE
    // (UblGenerator) como por la salida XML (cbc:IssueDate/cbc:IssueTime en cada estrategia) para
    // que ambos coincidan siempre.
    public static class DianTimeFormat
    {
        public static DateTime ToBogota(DateTime utcOrEquivalent) => utcOrEquivalent.AddHours(-5);

        public static string IssueDate(DateTime dt) => ToBogota(dt).ToString("yyyy-MM-dd");

        public static string IssueTime(DateTime dt) => ToBogota(dt).ToString("HH:mm:ss") + "-05:00";
    }
}
