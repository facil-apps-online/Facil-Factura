using System;

namespace Fel.Core.Entities
{
    public class ClientDocumentSetting
    {
        public Guid Id { get; set; }
        
        public Guid ClientId { get; set; }
        public Client Client { get; set; } = null!;

        public Guid? DocumentTypeId { get; set; }
        public DocumentType? DocumentType { get; set; }
        
        public Guid SelectedTemplateId { get; set; }
        public DocumentTemplate SelectedTemplate { get; set; } = null!;
        
        public DateTime UpdatedAt { get; set; }
    }
}
