using System.Security.Cryptography.X509Certificates;

namespace Fel.Core.Interfaces
{
    public interface IXmlSigner
    {
        /// <summary>
        /// Aplica la firma digital XAdES-EPES al XML de la DIAN.
        /// </summary>
        /// <param name="xmlContent">El XML UBL 2.1 generado</param>
        /// <param name="certificate">El certificado extraído de la Bóveda</param>
        /// <returns>El XML firmado listo para enviar por SOAP</returns>
        string SignXml(string xmlContent, X509Certificate2 certificate);
    }
}
