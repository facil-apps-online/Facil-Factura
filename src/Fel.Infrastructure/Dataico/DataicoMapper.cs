using System.Collections.Generic;
using Fel.Core.Entities;
using Fel.Infrastructure.Dataico.Models;
using Fel.Infrastructure.Dian;

namespace Fel.Infrastructure.Dataico
{
    // Traducciones mecánicas y estables (catálogos DIAN oficiales) de nuestros códigos internos
    // al vocabulario de palabras que usa la API de Dataico. Los campos que dependen del perfil
    // fiscal propio del tercero (tax_level_code, regimen) o de la operación (payment_means,
    // payment_means_type) NO se adivinan aquí: el tenant/cliente los captura explícitamente,
    // porque Dataico no documenta un catálogo cerrado y equivocarlos afecta un documento legal.
    public static class DataicoMapper
    {
        // Códigos cortos reales que acepta la API de Dataico para party_identification_type
        // (confirmados contra el error 'Tiene que ser uno de estos valores: [CC CE IE NIT
        // NIT_OTRO_PAIS NUIP PASAPORTE PEP PPT RC TE TI]' — los nombres largos tipo
        // "CEDULA_DE_CIUDADANIA" que había antes no existen en el catálogo real de Dataico).
        private static readonly Dictionary<string, string> IdentificationTypeMap = new()
        {
            ["11"] = "RC",
            ["12"] = "TI",
            ["13"] = "CC",
            ["21"] = "TE",
            ["22"] = "CE",
            ["31"] = "NIT",
            ["41"] = "PASAPORTE",
            ["42"] = "IE",
            ["50"] = "NIT_OTRO_PAIS",
            ["91"] = "NUIP"
        };

        // overrides permite que el llamador use la equivalencia administrable desde el catálogo de
        // Tipos de Identificación (IdentificationType.DataicoCode) en vez de esta tabla fija — por
        // ejemplo si Dataico agrega PEP/PPT y el admin los captura ahí sin necesitar un despliegue.
        public static string MapIdentificationType(string dianCode, IReadOnlyDictionary<string, string>? overrides = null)
        {
            if (overrides != null && overrides.TryGetValue(dianCode ?? string.Empty, out var overridden) && !string.IsNullOrWhiteSpace(overridden))
                return overridden;

            return IdentificationTypeMap.TryGetValue(dianCode ?? string.Empty, out var mapped) ? mapped : (dianCode ?? string.Empty);
        }

        // NIT (31) y NIT de otro país (50) identifican personas jurídicas; cualquier otro documento, personas naturales.
        public static string MapPartyType(string dianIdentificationTypeCode) =>
            dianIdentificationTypeCode is "31" or "50" ? "PERSONA_JURIDICA" : "PERSONA_NATURAL";

        // Nuestro CityCode es el código DANE completo (depto+municipio, ej. "11001" Bogotá);
        // Dataico espera esos dos segmentos por separado.
        public static (string Department, string City) SplitCityCode(string? cityCode)
        {
            if (string.IsNullOrWhiteSpace(cityCode) || cityCode.Length < 5)
            {
                return (string.Empty, string.Empty);
            }

            return (cityCode.Substring(0, 2), cityCode.Substring(2));
        }

        // omitIncompleteAddress: el documento soporte sale sin dirección cuando el proveedor colombiano tiene dirección pero no ciudad DANE (Dataico rechaza
        // el cliente con una dirección sin departamento y ciudad); el tercero conserva su dirección guardada.
        public static DataicoParty ToDataicoParty(Customer customer, IReadOnlyDictionary<string, string>? identificationTypeOverrides = null, bool omitIncompleteAddress = false)
        {
            // Fuera de Colombia no hay código DANE: el departamento o estado y la ciudad viajan como texto, con el país del tercero.
            var isForeign = !string.IsNullOrWhiteSpace(customer.CountryCode) && customer.CountryCode != "CO";
            var (department, city) = isForeign ? (customer.StateName ?? string.Empty, customer.CityName ?? string.Empty) : SplitCityCode(customer.CityCode);
            var isJuridica = customer.IdentificationType is "31" or "50";

            // Persona jurídica: solo razón social. Persona natural: nombres y apellidos por
            // separado (Dataico distingue company_name de first_name/family_name); si el tercero
            // no tiene los campos discriminados (registros antiguos), se usa Name completo como
            // respaldo para no dejar el documento sin nombre del todo.
            var firstName = Join(customer.FirstName, customer.SecondName);
            var familyName = Join(customer.FirstLastName, customer.SecondLastName);
            if (!isJuridica && string.IsNullOrWhiteSpace(firstName) && string.IsNullOrWhiteSpace(familyName))
            {
                firstName = customer.Name;
            }

            // La dirección y el teléfono son opcionales en Dataico: sin dato no se envía la llave (un "" no equivale a omitirla). Un tercero colombiano sin
            // ninguna dirección tampoco manda el país: Dataico asume Colombia.
            static string? OrNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
            var hasAddress = !CustomerAddressRules.IsEmpty(customer.Address, customer.CityCode, customer.CityName, customer.StateName);
            if (omitIncompleteAddress && CustomerAddressRules.IncompleteCityMessage(isForeign, customer.Address, customer.CityCode, customer.CityName, customer.StateName) != null)
            {
                hasAddress = false;
                department = city = string.Empty;
            }

            return new DataicoParty
            {
                email = customer.Email,
                phone = OrNull(customer.Phone),
                party_identification_type = MapIdentificationType(customer.IdentificationType, identificationTypeOverrides),
                party_identification = customer.IdentificationNumber,
                party_type = MapPartyType(customer.IdentificationType),
                tax_level_code = customer.DataicoTaxLevelCode,
                // Régimen simple (47) manda SIMPLE aunque el catálogo de Dataico del tercero diga otra cosa.
                regimen = customer.IncomeTaxRegime == "47" ? "SIMPLE" : customer.DataicoRegimen,
                department = OrNull(department),
                city = OrNull(city),
                address_line = hasAddress || isForeign ? OrNull(customer.Address) : null,
                country_code = isForeign ? customer.CountryCode : (hasAddress ? "CO" : null),
                company_name = isJuridica ? customer.Name : string.Empty,
                first_name = isJuridica ? string.Empty : firstName,
                family_name = isJuridica ? string.Empty : familyName
            };
        }

        public static string Join(params string?[] parts) =>
            string.Join(" ", parts is null ? System.Array.Empty<string>() : System.Array.FindAll(parts, p => !string.IsNullOrWhiteSpace(p)));

        public static DataicoTax ToDataicoTax(ProductTax tax) => new()
        {
            tax_category = tax.TaxCategory,
            tax_rate = tax.Rate
        };
    }
}
