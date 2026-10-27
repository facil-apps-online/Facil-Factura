using System.Linq;

namespace Fel.Infrastructure.Dian
{
    // La dirección del adquirente es opcional (Anexo técnico 1.9: PhysicalLocation 0..1, salvo ventas fuera de establecimiento), pero si se informa el grupo
    // completo se valida: en Colombia exige un municipio DANE válido (FAK09). Por eso sin dirección el grupo se omite, y a medias se avisa antes de emitir.
    public static class CustomerAddressRules
    {
        // Sin ningún dato de ubicación: no se envía dirección.
        public static bool IsEmpty(string? address, string? cityCode, string? cityName, string? stateName) =>
            string.IsNullOrWhiteSpace(address) && string.IsNullOrWhiteSpace(cityCode) && string.IsNullOrWhiteSpace(cityName) && string.IsNullOrWhiteSpace(stateName);

        // Código DANE completo (departamento + municipio): 5 dígitos o más.
        public static bool IsValidDaneCode(string? cityCode) => cityCode != null && cityCode.Trim().Length >= 5 && cityCode.Trim().All(char.IsDigit);

        // Mensaje cuando hay datos de ubicación pero la ciudad de un tercero colombiano no está completa; null si todo está bien o no hay dirección.
        public static string? IncompleteCityMessage(bool isForeign, string? address, string? cityCode, string? cityName, string? stateName)
        {
            if (isForeign || IsEmpty(address, cityCode, cityName, stateName)) return null;
            return IsValidDaneCode(cityCode)
                ? null
                : "El tercero tiene dirección pero le falta la ciudad (código DANE). Complétala en el tercero o quita la dirección para emitir sin ella.";
        }
    }
}
