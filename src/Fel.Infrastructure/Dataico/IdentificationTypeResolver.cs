using System;
using System.Collections.Generic;
using System.Linq;
using Fel.Core.Entities;

namespace Fel.Infrastructure.Dataico
{
    // Encuentra el tipo de documento del catálogo (datos maestros) a partir de cómo esté guardado en un tercero: el código DIAN ("13"),
    // el corto de facturación de Dataico ("CC"), el largo de nómina ("CEDULA_DE_CIUDADANIA") o el nombre. Los empleados importados desde
    // Excel quedaron con el texto crudo de la columna, así que no siempre es el código DIAN.
    public static class IdentificationTypeResolver
    {
        public static IdentificationType? Find(IEnumerable<IdentificationType> catalog, string? value)
        {
            var text = value?.Trim();
            if (string.IsNullOrEmpty(text)) return null;
            bool Same(string? candidate) => !string.IsNullOrWhiteSpace(candidate) && string.Equals(candidate.Trim(), text, StringComparison.OrdinalIgnoreCase);

            var types = catalog.ToList();
            // El código DIAN gana si coincide: un valor corto como "NIT" existe en dos vocabularios con el mismo significado.
            return types.FirstOrDefault(t => Same(t.Code))
                ?? types.FirstOrDefault(t => Same(t.DataicoCode))
                ?? types.FirstOrDefault(t => Same(t.DataicoPayrollCode))
                ?? types.FirstOrDefault(t => Same(t.Name));
        }

        // Valor para la API de nómina de Dataico; null si el tipo no está en el catálogo o no tiene equivalencia de nómina.
        public static string? PayrollCode(IEnumerable<IdentificationType> catalog, string? value)
        {
            var code = Find(catalog, value)?.DataicoPayrollCode;
            return string.IsNullOrWhiteSpace(code) ? null : code.Trim();
        }
    }
}
