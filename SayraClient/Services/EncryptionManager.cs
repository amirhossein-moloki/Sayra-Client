using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;

namespace SayraClient.Services;

public class EncryptionManager
{
    private readonly ILogger<EncryptionManager> _logger;
    private readonly SessionKeyManager _sessionKeyManager;

    public EncryptionManager(ILogger<EncryptionManager> logger, SessionKeyManager sessionKeyManager)
    {
        _logger = logger;
        _sessionKeyManager = sessionKeyManager;
    }

    public string Encrypt(string plaintext)
    {
        byte[]? key = _sessionKeyManager.GetSessionKey();
        if (key == null) throw new InvalidOperationException("Session key not set.");

        using Aes aes = Aes.Create();
        aes.Key = key;
        aes.GenerateIV();

        using ICryptoTransform encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
        using MemoryStream ms = new();
        ms.Write(aes.IV, 0, aes.IV.Length); // Prepend IV to the ciphertext

        using (CryptoStream cs = new(ms, encryptor, CryptoStreamMode.Write))
        using (StreamWriter sw = new(cs))
        {
            sw.Write(plaintext);
        }

        return Convert.ToBase64String(ms.ToArray());
    }

    public string Decrypt(string ciphertextBase64)
    {
        byte[]? key = _sessionKeyManager.GetSessionKey();
        if (key == null) throw new InvalidOperationException("Session key not set.");

        byte[] fullCiphertext = Convert.FromBase64String(ciphertextBase64);

        using Aes aes = Aes.Create();
        aes.Key = key;

        byte[] iv = new byte[aes.BlockSize / 8];
        byte[] ciphertext = new byte[fullCiphertext.Length - iv.Length];

        Array.Copy(fullCiphertext, 0, iv, 0, iv.Length);
        Array.Copy(fullCiphertext, iv.Length, ciphertext, 0, ciphertext.Length);

        aes.IV = iv;

        using ICryptoTransform decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
        using MemoryStream ms = new(ciphertext);
        using CryptoStream cs = new(ms, decryptor, CryptoStreamMode.Read);
        using StreamReader sr = new(cs);

        return sr.ReadToEnd();
    }
}
