using System;

namespace Fel.Infrastructure.Dataico
{
    // Aproximación aritmética a 2 decimales según el Anexo Técnico de Factura Electrónica de Venta
    // (Resolución 000165 de 2023, v1.9), numeral 5.2.1 "Aproximaciones aritméticas", que Dataico
    // aplica al validar los montos que recibe (su mensaje "rondeo_dian"):
    //
    //   Dígito siguiente al menos significativo | Resultado
    //   0 a 4                                   | se mantiene el dígito menos significativo
    //   6 a 9                                   | se incrementa
    //   5 y el dígito que le sigue es 0 o par   | se mantiene
    //   5 y el dígito que le sigue es impar     | se incrementa
    //
    // NO es truncar (lo que se hacía antes con Math.Truncate: fallaba siempre que el tercer decimal
    // era 6-9, p. ej. 10398.826 daba 10398.82 y Dataico esperaba 10398.83) ni es el redondeo al par
    // de Math.Round (que mira el dígito que se conserva, no el siguiente al 5: 481.275 da 481.28 y la
    // DIAN exige 481.27, caso real ya rechazado).
    public static class DianRounding
    {
        public static decimal Round2(decimal value)
        {
            var sign = value < 0 ? -1m : 1m;
            var scaled = Math.Abs(value) * 100m;          // el dígito menos significativo pasa a ser la unidad
            var kept = Math.Floor(scaled);
            var fraction = scaled - kept;                  // 0 <= fraction < 1: son los decimales sobrantes

            var next = (int)Math.Floor(fraction * 10m);    // dígito siguiente al menos significativo
            if (next < 5) return sign * kept / 100m;
            if (next > 5) return sign * (kept + 1m) / 100m;

            var afterNext = (int)Math.Floor((fraction * 10m - 5m) * 10m); // el que le sigue al 5
            return sign * (afterNext % 2 == 0 ? kept : kept + 1m) / 100m;
        }
    }
}
