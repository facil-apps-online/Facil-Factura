using System;
using System.Text;

namespace Fel.Infrastructure.Services
{
    // Convierte un valor monetario a su representación en letras, en español, con la fórmula
    // colombiana usual para el total en palabras de una factura ("... PESOS M/CTE"). Soporta hasta
    // billones (10^12), más que suficiente para cualquier documento real.
    public static class NumberToWordsEs
    {
        private static readonly string[] Unidades =
        {
            "", "UN", "DOS", "TRES", "CUATRO", "CINCO", "SEIS", "SIETE", "OCHO", "NUEVE", "DIEZ",
            "ONCE", "DOCE", "TRECE", "CATORCE", "QUINCE", "DIECISÉIS", "DIECISIETE", "DIECIOCHO", "DIECINUEVE", "VEINTE"
        };

        private static readonly string[] Decenas =
        {
            "", "", "VEINTI", "TREINTA", "CUARENTA", "CINCUENTA", "SESENTA", "SETENTA", "OCHENTA", "NOVENTA"
        };

        private static readonly string[] Centenas =
        {
            "", "CIENTO", "DOSCIENTOS", "TRESCIENTOS", "CUATROCIENTOS", "QUINIENTOS",
            "SEISCIENTOS", "SETECIENTOS", "OCHOCIENTOS", "NOVECIENTOS"
        };

        public static string ConvertirPesos(decimal valor)
        {
            var entero = (long)Math.Truncate(Math.Abs(valor));
            var texto = entero == 0 ? "CERO" : ConvertirEntero(entero);
            return $"{texto} PESOS M/CTE";
        }

        private static string ConvertirEntero(long numero)
        {
            if (numero == 0) return "CERO";

            var billones = numero / 1_000_000_000_000;
            var millones = numero % 1_000_000_000_000 / 1_000_000;
            var miles = numero % 1_000_000 / 1_000;
            var resto = numero % 1_000;

            var sb = new StringBuilder();

            if (billones > 0)
            {
                sb.Append(billones == 1 ? "UN BILLÓN" : $"{ConvertirGrupo(billones)} BILLONES");
                sb.Append(' ');
            }
            if (millones > 0)
            {
                sb.Append(millones == 1 ? "UN MILLÓN" : $"{ConvertirGrupo(millones)} MILLONES");
                sb.Append(' ');
            }
            if (miles > 0)
            {
                sb.Append(miles == 1 ? "MIL" : $"{ConvertirGrupo(miles)} MIL");
                sb.Append(' ');
            }
            if (resto > 0)
            {
                sb.Append(ConvertirGrupo(resto));
            }

            return sb.ToString().Trim();
        }

        // Convierte un número de 1 a 999.
        private static string ConvertirGrupo(long numero)
        {
            if (numero == 100) return "CIEN";

            var centena = numero / 100;
            var restoCentena = numero % 100;
            var sb = new StringBuilder();

            if (centena > 0)
            {
                sb.Append(Centenas[centena]);
                sb.Append(' ');
            }

            if (restoCentena > 0)
            {
                if (restoCentena <= 20)
                {
                    sb.Append(Unidades[restoCentena]);
                }
                else
                {
                    var decena = restoCentena / 10;
                    var unidad = restoCentena % 10;
                    if (decena == 2)
                    {
                        sb.Append(unidad == 0 ? "VEINTE" : Decenas[2] + Unidades[unidad]);
                    }
                    else
                    {
                        sb.Append(Decenas[decena]);
                        if (unidad > 0)
                        {
                            sb.Append(" Y ");
                            sb.Append(Unidades[unidad]);
                        }
                    }
                }
            }

            return sb.ToString().Trim();
        }
    }
}
