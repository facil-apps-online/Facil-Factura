using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Fel.Core.Interfaces;
using Microsoft.Extensions.Configuration;

namespace Fel.Infrastructure.Security
{
    public class CryptoService : ICryptoService
    {
        private readonly byte[] _key;

        public CryptoService(IConfiguration configuration)
        {
            var keyString = configuration["MasterKey"] ?? throw new ArgumentNullException("MasterKey is missing in configuration");
            
            // Assuming MasterKey is a Base64 string of a 32-byte key
            _key = Convert.FromBase64String(keyString);
            
            if (_key.Length != 32)
            {
                throw new ArgumentException("MasterKey must be a valid 256-bit (32 bytes) key.");
            }
        }

        public string Encrypt(string plainText)
        {
            if (string.IsNullOrEmpty(plainText))
                return plainText;

            using Aes aesAlg = Aes.Create();
            aesAlg.Key = _key;
            aesAlg.GenerateIV();

            using MemoryStream msEncrypt = new MemoryStream();
            // Write the IV first so we can read it later
            msEncrypt.Write(aesAlg.IV, 0, aesAlg.IV.Length);

            using (CryptoStream csEncrypt = new CryptoStream(msEncrypt, aesAlg.CreateEncryptor(aesAlg.Key, aesAlg.IV), CryptoStreamMode.Write))
            using (StreamWriter swEncrypt = new StreamWriter(csEncrypt))
            {
                swEncrypt.Write(plainText);
            }

            return Convert.ToBase64String(msEncrypt.ToArray());
        }

        public string Decrypt(string cipherText)
        {
            if (string.IsNullOrEmpty(cipherText))
                return cipherText;

            byte[] fullCipher = Convert.FromBase64String(cipherText);

            using Aes aesAlg = Aes.Create();
            aesAlg.Key = _key;
            
            byte[] iv = new byte[aesAlg.BlockSize / 8];
            Array.Copy(fullCipher, 0, iv, 0, iv.Length);
            aesAlg.IV = iv;

            using MemoryStream msDecrypt = new MemoryStream(fullCipher, iv.Length, fullCipher.Length - iv.Length);
            using CryptoStream csDecrypt = new CryptoStream(msDecrypt, aesAlg.CreateDecryptor(aesAlg.Key, aesAlg.IV), CryptoStreamMode.Read);
            using StreamReader srDecrypt = new StreamReader(csDecrypt);

            return srDecrypt.ReadToEnd();
        }

        public string GenerateCufeSha384(string dataToHash)
        {
            if (string.IsNullOrEmpty(dataToHash))
                return string.Empty;

            using (SHA384 sha384Hash = SHA384.Create())
            {
                byte[] sourceBytes = Encoding.UTF8.GetBytes(dataToHash);
                byte[] hashBytes = sha384Hash.ComputeHash(sourceBytes);
                
                StringBuilder hashBuilder = new StringBuilder();
                foreach (byte b in hashBytes)
                {
                    hashBuilder.Append(b.ToString("x2"));
                }
                return hashBuilder.ToString();
            }
        }
    }
}
