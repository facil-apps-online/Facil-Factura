using System;

namespace Fel.Infrastructure.Ubl
{
    // Colombia no tiene horario de verano: UTC-5 fijo todo el año. Los DateTime que llegan a esta
    // capa ya vienen en hora de Colombia (Fel.Core.Models.ColombiaTime.Now): la fecha de emisión de
    // todo documento se genera y guarda en hora colombiana. Antes llegaban en UTC y aquí se les
    // restaban 5 horas; hacerlo ahora las restaría dos veces. La DIAN exige la hora en zona
    // horaria -5 (Anexo Técnico, regla FAD10 — "Debe ser informada la hora en una zona
    // horaria -5, que es la zona horaria oficial de Colombia"), por eso solo se agrega el sufijo
    // "-05:00". Único punto de formato, usado tanto por el cálculo de CUFE/CUDE
    // (UblGenerator) como por la salida XML (cbc:IssueDate/cbc:IssueTime en cada estrategia) para
    // que ambos coincidan siempre.
    public static class DianTimeFormat
    {
        public static DateTime ToBogota(DateTime colombiaTime) => colombiaTime;

        public static string IssueDate(DateTime dt) => ToBogota(dt).ToString("yyyy-MM-dd");

        public static string IssueTime(DateTime dt) => ToBogota(dt).ToString("HH:mm:ss") + "-05:00";
    }
}
