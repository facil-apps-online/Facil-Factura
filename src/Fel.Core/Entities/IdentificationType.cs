using System;

namespace Fel.Core.Entities
{
    // Catálogo de tipos de documento de identificación (CC, NIT, CE, etc.) según el Anexo
    // Técnico de la DIAN. Deliberadamente separado de DocumentType (que representa lo que
    // emitimos: facturas, notas, RIPS...) porque no tiene tarifa ni se referencia desde
    // Document — es solo un catálogo de referencia para identificar personas/empresas.
    public class IdentificationType
    {
        public Guid Id { get; set; }
        public string Code { get; set; } = string.Empty; // Código DIAN, ej. "13"
        public string Name { get; set; } = string.Empty; // ej. "Cédula de Ciudadanía"
        public bool IsActive { get; set; } = true;

        // Equivalencia del código DIAN en el vocabulario propio de Dataico (ej. "13" -> "CC").
        // Null/vacío si no se ha capturado — en ese caso el envío a Dataico usa su propia tabla
        // fija de respaldo (ver DataicoMapper). No aplica a la emisión directa DIAN, que usa Code.
        public string? DataicoCode { get; set; }
    }
}
