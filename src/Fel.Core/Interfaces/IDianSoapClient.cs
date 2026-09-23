using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

namespace Fel.Core.Interfaces
{
    public interface IDianSoapClient
    {
        /// <summary>
        /// Envía el XML firmado a la DIAN mediante SOAP seguro (WS-Security). El anexo técnico
        /// (numeral 7.8.2) exige que contentFile sea el arreglo de bytes de un .zip, no del XML
        /// suelto — este método recibe el XML ya firmado en texto plano y lo comprime.
        /// </summary>
        /// <param name="zipFileName">Nombre del .zip entregado a la DIAN (prefijo "z", numeral 6.5.8)</param>
        /// <param name="entryFileName">Nombre del XML dentro del .zip (prefijo "fv"/"nc"/"nd", numeral 6.5.7)</param>
        /// <param name="signedXml">XML ya firmado (UBLExtensions con la firma XAdES aplicada)</param>
        /// <param name="certificate">Certificado digital del emisor para firmar el sobre SOAP</param>
        /// <param name="environment">"1" = Producción, "2" = Habilitación (set de pruebas)</param>
        /// <returns>La respuesta SOAP (TrackId o ValidationResult)</returns>
        Task<string> SendBillAsync(string zipFileName, string entryFileName, string signedXml, X509Certificate2 certificate, string environment);

        /// <summary>
        /// Envía un documento de Nómina Electrónica (o su Nota de Ajuste) a la DIAN. A diferencia de
        /// <see cref="SendBillAsync"/>, la DIAN exige el XML firmado dentro de un .zip — este método
        /// recibe el XML ya firmado en texto plano y se encarga de comprimirlo antes de enviarlo.
        /// </summary>
        /// <param name="fileName">Nombre del XML dentro del .zip (e.g. NIE12345.xml)</param>
        /// <param name="signedXml">XML de nómina ya firmado (UBLExtensions con la firma XAdES aplicada)</param>
        Task<string> SendNominaSyncAsync(string fileName, string signedXml, X509Certificate2 certificate, string environment);

        /// <summary>
        /// Envía un Documento Equivalente Electrónico (tiquete POS, Resolución 000165 de 2023) a la
        /// DIAN. Igual que <see cref="SendNominaSyncAsync"/>, va comprimido en .zip, pero usa la
        /// operación SendBillSync (síncrona) en vez de SendNominaSync.
        /// </summary>
        Task<string> SendDocumentSyncAsync(string fileName, string signedXml, X509Certificate2 certificate, string environment);

        /// <summary>
        /// Envía un Evento de Recepción RADIAN (ApplicationResponse: Acuse, Recibo del bien,
        /// Aceptación, Reclamo) a la DIAN. Igual que los anteriores, va comprimido en .zip, con la
        /// operación SendEventUpdateStatus.
        /// </summary>
        Task<string> SendEventUpdateStatusAsync(string fileName, string signedXml, X509Certificate2 certificate, string environment);

        /// <summary>
        /// Consulta el estado de un documento enviado previamente de manera segura.
        /// </summary>
        Task<string> GetStatusAsync(string trackId, X509Certificate2 certificate, string environment);

        /// <summary>
        /// Envía un documento del Set de Pruebas de habilitación — operación SOAP distinta de
        /// <see cref="SendBillAsync"/>: igual que Nómina/Documento Equivalente va comprimido en
        /// .zip, y además exige el TestSetId que la DIAN asignó al registrar el software (ver
        /// DianHabilitationScraperService). Solo tiene sentido con environment="2" (habilitación).
        /// </summary>
        /// <param name="zipFileName">Nombre del .zip entregado a la DIAN (prefijo "z", numeral 6.5.8)</param>
        /// <param name="entryFileName">Nombre del XML dentro del .zip (prefijo "fv"/"nc"/"nd", numeral 6.5.7)</param>
        Task<string> SendTestSetAsync(string zipFileName, string entryFileName, string signedXml, string testSetId, X509Certificate2 certificate, string environment);

        /// <summary>
        /// Consulta el resultado de un envío hecho con <see cref="SendTestSetAsync"/> — operación
        /// separada de <see cref="GetStatusAsync"/> porque un .zip de set de pruebas puede contener
        /// más de un documento.
        /// </summary>
        Task<string> GetStatusZipAsync(string trackId, X509Certificate2 certificate, string environment);
    }
}
