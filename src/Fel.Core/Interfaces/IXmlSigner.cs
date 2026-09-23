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
        /// <param name="certificateChain">
        /// Cadena completa del .p12 (titular + intermedio(s) + raíz), si está disponible. Se embebe
        /// completa en el KeyInfo de la firma — sin ella, quien valida no puede construir la cadena
        /// de confianza hasta una raíz conocida. Si se omite, se firma solo con el certificado del
        /// titular (comportamiento anterior).
        /// </param>
        /// <returns>El XML firmado listo para enviar por SOAP</returns>
        string SignXml(string xmlContent, X509Certificate2 certificate, X509Certificate2Collection? certificateChain = null);
    }
}
