using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Fel.Core.Entities
{
    public class RipsCupsRule
    {
        [Key]
        [MaxLength(20)]
        public string Code { get; set; } = string.Empty;

        [Required]
        [MaxLength(500)]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// A: Ambos, M: Masculino, F: Femenino
        /// </summary>
        [MaxLength(2)]
        public string AllowedGender { get; set; } = string.Empty;

        public int MinAgeDays { get; set; }
        public int MaxAgeDays { get; set; }

        public bool RequiresDiagnosis { get; set; }
    }
}
