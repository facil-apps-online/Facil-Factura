using System;

namespace Fel.Core.Entities
{
    public class DocumentTemplate
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string RepxTemplateKey { get; set; } = string.Empty; // ID real en facil-reporting-api
        
        public TemplateStatus Status { get; set; } = TemplateStatus.Draft;
        public int VersionNumber { get; set; } = 1;
        
        public Guid? PreviousVersionId { get; set; } // Historial de linaje de versiones
        public DocumentTemplate? PreviousVersion { get; set; }
        
        public Guid? ClonedFromId { get; set; } // Si un tenant clona del superadmin
        public DocumentTemplate? ClonedFrom { get; set; }

        public Guid? DocumentTypeId { get; set; }
        public DocumentType? DocumentType { get; set; }
        
        public Guid? TenantId { get; set; }
        public Tenant? Tenant { get; set; }

        public Guid? ClientId { get; set; }
        public Client? Client { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
