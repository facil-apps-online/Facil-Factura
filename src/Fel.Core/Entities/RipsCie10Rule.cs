using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Fel.Core.Entities
{
    public class RipsCie10Rule
    {
        [Key]
        [MaxLength(20)]
        public string Code { get; set; } = string.Empty;

        [Required]
        [MaxLength(500)]
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// A: Ambos, M: Masculino, F: Femenino
        /// </summary>
        [MaxLength(2)]
        public string AllowedGender { get; set; } = string.Empty;

        // En días, no en años: el catálogo oficial (LIM_INF/LIM_SUP) codifica el límite como
        // unidad+valor (1=Horas, 2=Días, 3=Meses, 4=Años) — hay diagnósticos con límites reales
        // de pocos días (ej. tétanos neonatal, máximo 27 días) que un entero en años no puede
        // representar sin perder precisión.
        public int MinAgeDays { get; set; }
        public int MaxAgeDays { get; set; }
    }
}
