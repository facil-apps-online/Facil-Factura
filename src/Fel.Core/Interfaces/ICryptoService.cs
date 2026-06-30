namespace Fel.Core.Interfaces
{
    public interface ICryptoService
    {
        string Encrypt(string plainText);
        string Decrypt(string cipherText);
        string GenerateCufeSha384(string dataToHash);
    }
}
