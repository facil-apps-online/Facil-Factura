using System;
using System.Security.Cryptography.X509Certificates;

namespace Fel.Core.Interfaces
{
    public interface ICryptoVault
    {
        string EncryptPassword(string plainTextPassword);
        string DecryptPassword(string encryptedPassword);
        
        /// <summary>
        /// Obtiene el certificado digital desencriptado y listo para firmar.
        /// </summary>
        X509Certificate2 GetCertificate(string filePath, string encryptedPassword);

        /// <summary>
        /// Obtiene la cadena completa de certificados del .p12 (certificado del titular +
        /// intermedio(s) de la CA + raíz, si el archivo los trae empaquetados). Necesaria para que
        /// la firma XAdES embeba la cadena completa en KeyInfo — sin ella, quien valida la firma no
        /// puede construir la cadena de confianza hasta una raíz conocida (falla con "unable to get
        /// local issuer certificate"), que es lo que causaba el rechazo DIAN "ZE02: Valida firma del
        /// documento" pese a que la firma en si era criptograficamente correcta.
        /// </summary>
        X509Certificate2Collection GetCertificateChain(string filePath, string encryptedPassword);

        /// <summary>
        /// Extrae el NIT del titular desde el atributo SERIALNUMBER (OID 2.5.4.5) del Subject del
        /// certificado — así lo llevan los certificados colombianos de firma digital para
        /// facturación electrónica. Devuelve null si el certificado no trae ese atributo.
        /// </summary>
        string? ExtractNit(X509Certificate2 certificate);
    }
}
