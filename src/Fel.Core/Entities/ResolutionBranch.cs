using System;

namespace Fel.Core.Entities
{
    // Sucursales en las que se puede usar una resolución. La resolución sigue siendo del Client (el consecutivo es uno
    // solo por resolución), así que una misma resolución puede estar disponible en varias sucursales.
    public class ResolutionBranch
    {
        public Guid ResolutionId { get; set; }
        public Resolution Resolution { get; set; } = null!;

        public Guid BranchId { get; set; }
        public Branch Branch { get; set; } = null!;
    }
}
