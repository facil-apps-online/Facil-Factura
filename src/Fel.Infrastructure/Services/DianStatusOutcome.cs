using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace Fel.Infrastructure.Services
{
    // Resultado REAL de la validacion de un documento por la DIAN, que es distinto de "se transmitio
    // bien". SendTestSetAsync solo devuelve un ZipKey confirmando recepcion; la aceptacion o rechazo
    // llega despues, consultando GetStatusZip con ese mismo ZipKey.
    //
    // La diferencia no es teorica: durante la habilitacion de SoFactory (septiembre 2026) seis
    // documentos fueron recibidos correctamente por la DIAN y rechazados todos con la regla ZE02.
    // Contarlos como "enviados exitosamente" habria agotado el presupuesto de intentos sin que
    // ninguno contara para el set de pruebas.
    public class DianStatusOutcome
    {
        // true solo si la DIAN acepto el documento (<b:IsValid>true</b:IsValid>).
        public bool Accepted { get; init; }

        // false mientras la DIAN todavia no termina de validar: hay que volver a consultar.
        public bool Resolved { get; init; } = true;

        public string StatusDescription { get; init; } = string.Empty;

        // Reglas que reporto la DIAN, tal cual: "Regla: ZE02, Rechazo: ...". Incluye tanto rechazos
        // (bloquean la aceptacion) como notificaciones (no la bloquean).
        public IReadOnlyList<string> Rules { get; init; } = Array.Empty<string>();

        public string RejectionSummary =>
            Rules.Count == 0
                ? StatusDescription
                : string.Join(" | ", Rules.Where(r => r.Contains("Rechazo", StringComparison.OrdinalIgnoreCase)));

        public static DianStatusOutcome Pending() =>
            new() { Accepted = false, Resolved = false, StatusDescription = "La DIAN aún está validando el documento." };

        // Parsea la respuesta SOAP cruda de GetStatusZip. Se hace con XDocument y no con string
        // matching porque la respuesta trae namespaces y anidamiento reales, y un cambio menor de
        // formato del lado de la DIAN no deberia darnos un falso "aceptado".
        public static DianStatusOutcome FromGetStatusZipResponse(string soapResponse)
        {
            if (string.IsNullOrWhiteSpace(soapResponse))
                return Pending();

            XDocument doc;
            try
            {
                doc = XDocument.Parse(soapResponse);
            }
            catch (System.Xml.XmlException)
            {
                return new DianStatusOutcome
                {
                    Accepted = false,
                    StatusDescription = "La respuesta de la DIAN no es XML válido."
                };
            }

            // Los elementos van bajo el namespace del contrato DianResponse; se buscan por nombre
            // local para no acoplarnos a la URI exacta.
            string? Local(string name) => doc.Descendants()
                .FirstOrDefault(e => e.Name.LocalName == name)?.Value;

            var rules = doc.Descendants()
                .Where(e => e.Name.LocalName == "string")
                .Select(e => e.Value.Trim())
                .Where(v => v.Length > 0)
                .ToList();

            var isValidRaw = Local("IsValid");
            var statusDescription = Local("StatusDescription") ?? string.Empty;

            // Sin IsValid todavia no hay veredicto: la DIAN sigue procesando el zip.
            if (string.IsNullOrWhiteSpace(isValidRaw))
                return Pending();

            return new DianStatusOutcome
            {
                Accepted = bool.TryParse(isValidRaw, out var ok) && ok,
                Resolved = true,
                StatusDescription = statusDescription,
                Rules = rules
            };
        }
    }
}
