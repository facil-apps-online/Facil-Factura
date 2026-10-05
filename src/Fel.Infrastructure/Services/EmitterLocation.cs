using Fel.Core.Entities;

namespace Fel.Infrastructure.Services
{
    // Dónde emite el documento: la dirección y los datos de contacto que salen en el XML y en el PDF. Son los de la sucursal
    // cuando tiene dirección propia, y los del Client cuando no (la sucursal principal hereda). Resolverlo en un solo lugar
    // evita que cada mapper decida por su cuenta.
    public sealed record EmitterLocation(
        string Address,
        string City,
        string? CityCode,
        string Phone,
        string Email,
        string? BranchName,
        string? BranchCode)
    {
        public static EmitterLocation For(Client client, Branch? branch)
        {
            // Con dirección propia se usan juntas dirección, ciudad y código de ciudad: nunca se mezcla la ciudad de una
            // con la dirección de otra. Teléfono y correo sí caen al Client cuando la sucursal no los tiene.
            var hasOwnAddress = branch != null && !string.IsNullOrWhiteSpace(branch.Address);

            return new EmitterLocation(
                Address: hasOwnAddress ? branch!.Address! : client.Address,
                City: hasOwnAddress ? (branch!.City ?? string.Empty) : client.City,
                CityCode: hasOwnAddress ? branch!.CityCode : client.CityCode,
                Phone: !string.IsNullOrWhiteSpace(branch?.Phone) ? branch!.Phone! : client.Phone,
                Email: !string.IsNullOrWhiteSpace(branch?.Email) ? branch!.Email! : client.Email,
                BranchName: branch?.Name,
                BranchCode: branch?.Code);
        }

        // Ubicación del Client sin sucursal (habilitación ante la DIAN, que es del Client y no de una sucursal).
        public static EmitterLocation ForClient(Client client) => For(client, null);
    }
}
