using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

namespace Fel.Core.Interfaces
{
    public interface IDianSoapClient
    {
        /// <summary>
        /// Envía el XML firmado a la DIAN mediante SOAP seguro (WS-Security).
        /// </summary>
        /// <param name="fileName">Nombre del archivo (e.g. fe123456789.xml)</param>
        /// <param name="xmlContentBase64">Contenido del XML firmado codificado en Base64</param>
        /// <param name="certificate">Certificado digital del emisor para firmar el sobre SOAP</param>
        /// <returns>La respuesta SOAP (TrackId o ValidationResult)</returns>
        Task<string> SendBillAsync(string fileName, string xmlContentBase64, X509Certificate2 certificate);
        
        /// <summary>
        /// Consulta el estado de un documento enviado previamente de manera segura.
        /// </summary>
        Task<string> GetStatusAsync(string trackId, X509Certificate2 certificate);
    }
}
