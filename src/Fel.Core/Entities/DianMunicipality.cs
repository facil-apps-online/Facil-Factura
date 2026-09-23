namespace Fel.Core.Entities
{
    // Catálogo de municipios del DANE: código de 5 posiciones (2 de departamento + 3 de
    // municipio). Lo puebla Superadmin con la fuente oficial del DANE; de acá salen los nombres
    // reales de ciudad/departamento que hoy faltan en el XML UBL de la DIAN (Client/Customer solo
    // guardan el código). No incluye la extensión de subzonas del DANE, solo el código base de 5.
    public class DianMunicipality
    {
        public string Code { get; set; } = string.Empty; // 5 dígitos DANE
        public string Name { get; set; } = string.Empty; // Nombre del municipio
        public string DepartmentCode { get; set; } = string.Empty; // Primeros 2 dígitos de Code
        public string DepartmentName { get; set; } = string.Empty;
    }
}
