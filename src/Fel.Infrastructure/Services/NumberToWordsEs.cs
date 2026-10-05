using System;
using System.Text;

namespace Fel.Infrastructure.Services
{
    // Convierte un valor monetario a su representación en letras, en español, con la fórmula
    // colombiana usual para el total en palabras de una factura.
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

            // Cada escala se descompone en grupos de 1 a 999. Así, por ejemplo,
            // 2.749 millones se procesa como grupos independientes y nunca se
            // envía 2749 a ConvertirGrupo.
            var partes = new StringBuilder();
            var billones = numero / 1_000_000_000_000;
            var milesDeMillones = numero % 1_000_000_000_000 / 1_000_000_000;
            var millones = numero % 1_000_000_000 / 1_000_000;
            var miles = numero % 1_000_000 / 1_000;
            var resto = numero % 1_000;

            if (billones > 0)
                AgregarParte(partes, billones == 1 ? "UN BILLÓN" : $"{ConvertirGrupo(billones)} BILLONES");

            if (milesDeMillones > 0)
                AgregarParte(partes, milesDeMillones == 1
                    ? "MIL MILLONES"
                    : $"{ConvertirGrupo(milesDeMillones)} MIL MILLONES");

            if (millones > 0)
                AgregarParte(partes, millones == 1 ? "UN MILLÓN" : $"{ConvertirGrupo(millones)} MILLONES");

            if (miles > 0)
                AgregarParte(partes, miles == 1 ? "MIL" : $"{ConvertirGrupo(miles)} MIL");

            if (resto > 0)
                AgregarParte(partes, ConvertirGrupo(resto));

            return partes.ToString();
        }

        private static void AgregarParte(StringBuilder texto, string parte)
        {
            if (texto.Length > 0) texto.Append(' ');
            texto.Append(parte);
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
