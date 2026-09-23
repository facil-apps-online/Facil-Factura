using System;
using System.Collections.Generic;

namespace Fel.Infrastructure.Services
{
    // Fotografía de solo lectura del estado real del cliente en los DOS portales de la DIAN.
    //
    // Deliberadamente no sabe nada de nuestra base de datos: son los hechos tal como están en la
    // DIAN. Cruzarlos con lo que tenemos guardado (certificado, resoluciones, contadores) y decidir
    // qué pasos faltan es responsabilidad de la capa de arriba, que sí conoce ambos lados.
    public class DianDiagnostico
    {
        public bool IsSuccess { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;

        /// <summary>Id interno del contribuyente en el portal (no es el NIT).</summary>
        public string ContributorId { get; set; } = string.Empty;
        public string Nit { get; set; } = string.Empty;
        public string RazonSocial { get; set; } = string.Empty;

        /// <summary>Campo "Estado de aprobación" de la ficha, ej. "Habilitado".</summary>
        public string EstadoAprobacion { get; set; } = string.Empty;

        /// <summary>
        /// Campo "Fecha de inicio producción". Vacío = el contribuyente NUNCA se ha sincronizado a
        /// producción, así que ese paso está pendiente.
        /// </summary>
        public string FechaInicioProduccion { get; set; } = string.Empty;
        public bool SincronizadoAProduccion => !string.IsNullOrWhiteSpace(FechaInicioProduccion);

        /// <summary>Modos de operación asociados en habilitación.</summary>
        public List<ModoDeOperacion> ModosEnHabilitacion { get; set; } = new();

        /// <summary>Modos de operación visibles en producción (reflejo de la sincronización).</summary>
        public List<ModoDeOperacion> ModosEnProduccion { get; set; } = new();

        /// <summary>Asociaciones prefijo ↔ software leídas del portal de producción.</summary>
        public List<PrefijoAsociado> PrefijosAsociados { get; set; } = new();

        /// <summary>El software propio que nos pertenece, si ya está registrado.</summary>
        public ModoDeOperacion? NuestroSoftware { get; set; }
    }

    public class ModoDeOperacion
    {
        public string Modo { get; set; } = string.Empty;        // "Software propio", "Software de un proveedor tecnológico"
        public string FechaRegistro { get; set; } = string.Empty;
        public string Estado { get; set; } = string.Empty;      // "Aceptado", "En proceso"
        public string NombreSoftware { get; set; } = string.Empty;
        public string SoftwareId { get; set; } = string.Empty;
        public string Pin { get; set; } = string.Empty;
    }

    public class PrefijoAsociado
    {
        public string Proveedor { get; set; } = string.Empty;
        public string NombreSoftware { get; set; } = string.Empty;
        public string SoftwareId { get; set; } = string.Empty;
        public string TipoDocumento { get; set; } = string.Empty;   // "01 - Factura Electronica"
        public string PrefijoYResolucion { get; set; } = string.Empty; // "SFRY - 18764092013879 (1000 - 2000)"
        public string FechaAsociacion { get; set; } = string.Empty;
        public string FechaExpiracion { get; set; } = string.Empty;

        /// <summary>Clave que usa el portal para identificar la fila: "prefijo|tipoDoc|resolución".</summary>
        public string RowKey { get; set; } = string.Empty;

        public string Prefijo => RowKey.Split('|').Length > 0 ? RowKey.Split('|')[0] : string.Empty;
        public string NumeroResolucion => RowKey.Split('|').Length > 2 ? RowKey.Split('|')[2] : string.Empty;
    }
}
