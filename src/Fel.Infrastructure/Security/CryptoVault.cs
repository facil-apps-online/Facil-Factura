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
            using (Aes aes = Aes.Create())
            {
                aes.Key = Convert.FromBase64String(_masterKey);
                aes.GenerateIV(); // IV aleatorio por cada cifrado — nunca reutilizar uno fijo con CBC,
                                   // o textos iguales (ej. dos certificados con la misma contraseña)
                                   // producen el mismo cifrado, filtrando esa coincidencia.

                ICryptoTransform encryptor = aes.CreateEncryptor(aes.Key, aes.IV);

                using (MemoryStream memoryStream = new MemoryStream())
                {
                    // El IV no es secreto — se guarda al inicio del blob cifrado para poder
                    // recuperarlo al descifrar, patrón estándar (IV || ciphertext).
                    memoryStream.Write(aes.IV, 0, aes.IV.Length);

                    using (CryptoStream cryptoStream = new CryptoStream((Stream)memoryStream, encryptor, CryptoStreamMode.Write, leaveOpen: true))
                    {
                        using (StreamWriter streamWriter = new StreamWriter((Stream)cryptoStream))
                        {
                            streamWriter.Write(plainTextPassword);
                        }
                    }

                    return Convert.ToBase64String(memoryStream.ToArray());
                }
            }
        }

        public string DecryptPassword(string encryptedPassword)
        {
            byte[] buffer = Convert.FromBase64String(encryptedPassword);

            using (Aes aes = Aes.Create())
            {
                aes.Key = Convert.FromBase64String(_masterKey);
                byte[] iv = new byte[16];
                Array.Copy(buffer, 0, iv, 0, iv.Length);
                aes.IV = iv;

                ICryptoTransform decryptor = aes.CreateDecryptor(aes.Key, aes.IV);

                using (MemoryStream memoryStream = new MemoryStream(buffer, iv.Length, buffer.Length - iv.Length))
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

        public X509Certificate2Collection GetCertificateChain(string filePath, string encryptedPassword)
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"El certificado .p12 no se encontró en la ruta especificada: {filePath}");
            }

            string plainPassword = DecryptPassword(encryptedPassword);

            var collection = new X509Certificate2Collection();
            collection.Import(filePath, plainPassword, X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.Exportable);
            return collection;
        }

        public string? ExtractNit(X509Certificate2 certificate)
        {
            const string SerialNumberOid = "2.5.4.5";

            foreach (var rdn in certificate.SubjectName.EnumerateRelativeDistinguishedNames())
            {
                if (rdn.GetSingleElementType().Value == SerialNumberOid)
                {
                    return rdn.GetSingleElementValue();
                }
            }

            return null;
        }
    }
}
