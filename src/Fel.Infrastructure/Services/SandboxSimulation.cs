using System;
using System.Security.Cryptography;

namespace Fel.Infrastructure.Services
{
    // Respuestas simuladas para los Clients de prueba de developers (Client.IsDeveloperSandbox).
    //
    // Un Client sandbox nunca va a tener los prerrequisitos reales — no hay certificado digital
    // cargado, no hay software registrado ante la DIAN, no hay habilitación ante el MinSalud — así
    // que el error honesto ("este emisor no tiene un certificado digital activo", o un 501 "no
    // implementado") deja la API inservible para quien está construyendo una integración contra
    // ella. El developer necesita una respuesta bien formada contra la cual programar.
    //
    // El patrón lo estrenó MinSaludMuvService.SendRipsAsync para los RIPS y es el que se replica
    // acá. Tiene cuatro partes, y las cuatro importan:
    //
    //   1. El corte va ANTES de cualquier validación que fuera a fallar.
    //   2. Se arma igual el payload real que se habría transmitido y se le devuelve al llamador,
    //      porque es lo que hace útil al sandbox: el dev ve el XML exacto que producen sus datos.
    //   3. El identificador que se devuelve es falso pero bien formado, para que el parseo y las
    //      validaciones del lado del cliente ejerciten el camino real.
    //   4. La respuesta dice explícitamente que es simulada, para que nadie confunda una
    //      aceptación de sandbox con una aceptación de verdad.
    //
    // Ojo con una diferencia respecto a los RIPS: allá el CUV se inventa entero porque lo emite el
    // Ministerio. Acá el CUFE/CUDE/CUNE NO se inventa — se calcula de verdad con UblGenerator, que
    // es determinístico y no necesita certificado. Así el developer recibe un identificador que sí
    // corresponde a sus datos y coincide con el que saldría en producción. Lo único simulado es la
    // aceptación de la DIAN, que es lo que exige firma y llamada SOAP.
    public static class SandboxSimulation
    {
        // El CUFE incluye la clave técnica de la resolución (numeral 11.2 del anexo técnico). Un
        // Client de prueba normalmente no tiene una, así que se usa este relleno y se avisa en el
        // mensaje: sin ese aviso alguien podría comparar el CUFE simulado contra el de producción
        // y no entender por qué no coincide.
        public const string ClaveTecnicaDeRelleno = "00000000000000000000000000000000000000000000000000000000000000000000000000000000";

        // Mismo formato que devuelve la DIAN: 96 caracteres hexadecimales.
        public static string NuevoTrackId() =>
            Convert.ToHexString(RandomNumberGenerator.GetBytes(48)).ToLowerInvariant();

        /// <summary>
        /// Identificador ficticio con la forma de un CUFE/CUDE/CUNE (96 hexadecimales), para los
        /// endpoints que todavía no tienen generación UBL propia y por tanto no pueden calcular
        /// uno de verdad.
        /// </summary>
        public static string NuevoIdentificadorFicticio() => NuevoTrackId();

        /// <summary>
        /// Mensaje para endpoints que aún no están implementados. Deja claras las dos cosas: que la
        /// respuesta es simulada, y que la funcionalidad real todavía no existe — para que nadie
        /// construya contra ella dando por hecho que ya funciona en producción.
        /// </summary>
        public static string MensajeNoImplementado(string queFalta) =>
            $"Respuesta simulada para Client de prueba. {queFalta} todavía no está implementado en "
            + "el API: la estructura de esta respuesta es la definitiva y se puede programar contra "
            + "ella, pero no se transmitió ni se transmitirá nada mientras la funcionalidad real no exista.";

        /// <summary>
        /// Resolución ficticia, NO persistida, para los Clients de prueba que no tienen una cargada.
        /// Sirve solo para que el mapper pueda armar el UBL y el developer vea el XML de su
        /// documento; nada de esto llega a la base ni a la DIAN.
        /// </summary>
        public static Fel.Core.Entities.Resolution ResolucionDePrueba(
            Guid clientId, string documentType, string? prefix, long numeroDocumento)
        {
            var ahora = DateTime.UtcNow;
            return new Fel.Core.Entities.Resolution
            {
                Id = Guid.Empty,
                ClientId = clientId,
                ResolutionNumber = "00000000000000000000",
                Prefix = prefix ?? "SETP",
                NumberStart = 1,
                NumberEnd = long.MaxValue,
                NextNumber = numeroDocumento,
                ValidFrom = ahora.AddYears(-1),
                ValidTo = ahora.AddYears(1),
                TechnicalKey = ClaveTecnicaDeRelleno,
                DocumentType = documentType,
                IsActive = true,
                IsDefault = false
            };
        }

        /// <summary>
        /// Mensaje que acompaña toda respuesta simulada. Siempre deja constancia de que el
        /// documento no se transmitió, y de si el identificador se calculó con la clave técnica
        /// real o con el relleno.
        /// </summary>
        public static string Mensaje(bool usoClaveTecnicaDeRelleno)
        {
            var baseMsg = "Documento aceptado (respuesta simulada — Client de prueba sin certificado "
                        + "digital, no se transmitió nada a la DIAN).";

            return usoClaveTecnicaDeRelleno
                ? baseMsg + " El identificador se calculó con una clave técnica de relleno porque la "
                          + "resolución del Client de prueba no tiene una, así que no coincidirá con "
                          + "el que produciría esa misma factura en producción."
                : baseMsg;
        }
    }
}
