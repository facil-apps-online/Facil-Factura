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
    }
}
