namespace Sayra.Client.Shared.Interfaces.Security
{
    public interface ICryptoService
    {
        byte[] Encrypt(byte[] plaintext, byte[] key, byte[] iv);
        byte[] Decrypt(byte[] ciphertext, byte[] key, byte[] iv);
        string EncryptString(string plaintext, string base64Key, string base64Iv);
        string DecryptString(string ciphertextBase64, string base64Key, string base64Iv);
    }
}
