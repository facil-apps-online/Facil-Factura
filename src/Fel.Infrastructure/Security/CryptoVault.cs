using System;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Fel.Core.Interfaces;
using Microsoft.Extensions.Configuration;

namespace Fel.Infrastructure.Security
{
    public class CryptoVault : ICryptoVault
    {
        private readonly string _masterKey;

        public CryptoVault(IConfiguration configuration)
        {
            // Debe ser una llave de 32 bytes (256 bits) en Base64
            _masterKey = configuration["Security:MasterKey"] ?? throw new ArgumentNullException("Security:MasterKey is missing in appsettings.json");
        }

        public string EncryptPassword(string plainTextPassword)
        {
            byte[] iv = new byte[16];
            byte[] array;

            using (Aes aes = Aes.Create())
            {
                aes.Key = Convert.FromBase64String(_masterKey);
                aes.IV = iv;

                ICryptoTransform encryptor = aes.CreateEncryptor(aes.Key, aes.IV);

                using (MemoryStream memoryStream = new MemoryStream())
                {
                    using (CryptoStream cryptoStream = new CryptoStream((Stream)memoryStream, encryptor, CryptoStreamMode.Write))
                    {
                        using (StreamWriter streamWriter = new StreamWriter((Stream)cryptoStream))
                        {
                            streamWriter.Write(plainTextPassword);
                        }

                        array = memoryStream.ToArray();
                    }
                }
            }

            return Convert.ToBase64String(array);
        }

        public string DecryptPassword(string encryptedPassword)
        {
            byte[] iv = new byte[16];
            byte[] buffer = Convert.FromBase64String(encryptedPassword);

            using (Aes aes = Aes.Create())
            {
                aes.Key = Convert.FromBase64String(_masterKey);
                aes.IV = iv;
                ICryptoTransform decryptor = aes.CreateDecryptor(aes.Key, aes.IV);

                using (MemoryStream memoryStream = new MemoryStream(buffer))
                {
                    using (CryptoStream cryptoStream = new CryptoStream((Stream)memoryStream, decryptor, CryptoStreamMode.Read))
                    {
                        using (StreamReader streamReader = new StreamReader((Stream)cryptoStream))
                        {
                            return streamReader.ReadToEnd();
                        }
                    }
                }
            }
        }

        public X509Certificate2 GetCertificate(string filePath, string encryptedPassword)
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"El certificado .p12 no se encontró en la ruta especificada: {filePath}");
            }

            string plainPassword = DecryptPassword(encryptedPassword);

            // Importante: MachineKeySet y Exportable para asegurar que la firma XAdES funcione correctamente en Windows/Linux.
            return new X509Certificate2(filePath, plainPassword, X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.Exportable);
        }
    }
}
