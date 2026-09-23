using System;

namespace Fel.Core.Entities
{
    public enum IntegratorKind
    {
        DirectDian = 0, // El propio Client tramita ante la DIAN con su SoftwareId/SoftwarePin
        ThirdPartyIntegrator = 1 // Un proveedor tecnológico (Dataico, u otro futuro) tramita en su nombre
    }

    // Catálogo de proveedores de documentos electrónicos. Reemplaza el enum cerrado
    // DocumentProvider: un integrador nuevo se da de alta como fila aquí (desde Superadmin, sin
    // deploy) en vez de requerir un valor de enum nuevo. La lógica de tramitación específica de
    // cada integrador sigue siendo código (una clase IDocumentSubmissionProvider por Code), pero
    // sus datos de identificación (nombre, NIT) y su tipo (directo DIAN vs. integrador externo)
    // son parametrizables.
    public class Integrator
    {
        public Guid Id { get; set; }
        public string Code { get; set; } = string.Empty; // "NATIVE", "DATAICO", ... — usado para resolver IDocumentSubmissionProvider
        public string Name { get; set; } = string.Empty;
        public string Nit { get; set; } = string.Empty;
        public IntegratorKind Kind { get; set; } = IntegratorKind.DirectDian;
        public bool IsActive { get; set; } = true;
    }
}
