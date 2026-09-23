using System;

namespace Fel.Core.Entities
{
    // Qué tipos de documento puede emitir un Client desde el formulario de facturación. Antes esto
    // era una lista fija en código (InvoiceController.SupportedDianCodes) igual para todos los
    // Clients de todos los Tenants; ahora cada Client tiene su propio set, que el Tenant administra
    // (ver TenantClientsController) y que se siembra con un set estándar al crear el Client.
    public class ClientEnabledDocumentType
    {
        public Guid Id { get; set; }
        public Guid ClientId { get; set; }
        public Client? Client { get; set; }

        public Guid DocumentTypeId { get; set; }
        public DocumentType? DocumentType { get; set; }
    }
}
