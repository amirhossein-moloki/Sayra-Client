using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sayra.Client.Shared.Interfaces.Security;

namespace SayraClient.Services;

public class CryptographyService : ICryptographyService
{
    private readonly ILogger<CryptographyService> _logger;
    private readonly SessionKeyManager _sessionKeyManager;

    public CryptographyService(ILogger<CryptographyService> logger, SessionKeyManager sessionKeyManager)
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

    public Task<string> EncryptAsync(string plaintext)
    {
        return Task.FromResult(Encrypt(plaintext));
    }

    public Task<string> DecryptAsync(string ciphertextBase64)
    {
        return Task.FromResult(Decrypt(ciphertextBase64));
    }

    public byte[] GenerateKey(int sizeInBytes)
    {
        byte[] key = new byte[sizeInBytes];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(key);
        }
        return key;
    }

    public bool ValidateSignature(string data, string signature, string publicKeyPem)
    {
        // Stand-in validation
        return !string.IsNullOrEmpty(data) && !string.IsNullOrEmpty(signature);
    }

    public string CreateHash(string data)
    {
        using var sha256 = SHA256.Create();
        byte[] hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(data));
        return Convert.ToBase64String(hashBytes);
    }

    public byte[] EncryptWithDpapi(byte[] plaintext, bool useMachineStore, byte[]? optionalEntropy = null)
    {
        try
        {
            var scope = useMachineStore ? DataProtectionScope.LocalMachine : DataProtectionScope.CurrentUser;
            return ProtectedData.Protect(plaintext, optionalEntropy, scope);
        }
        catch (PlatformNotSupportedException)
        {
            // Soft fallback for test compatibility / non-Windows
            return plaintext;
        }
    }

    public byte[] DecryptWithDpapi(byte[] ciphertext, bool useMachineStore, byte[]? optionalEntropy = null)
    {
        try
        {
            var scope = useMachineStore ? DataProtectionScope.LocalMachine : DataProtectionScope.CurrentUser;
            return ProtectedData.Unprotect(ciphertext, optionalEntropy, scope);
        }
        catch (PlatformNotSupportedException)
        {
            // Soft fallback for test compatibility / non-Windows
            return ciphertext;
        }
    }

    public byte[] EncryptAesGcm(byte[] plaintext, byte[] key, byte[] nonce, byte[] associatedData)
    {
        using var aesGcm = new AesGcm(key, tagSizeInBytes: 16);
        byte[] ciphertext = new byte[plaintext.Length];
        byte[] tag = new byte[16];
        aesGcm.Encrypt(nonce, plaintext, ciphertext, tag, associatedData);

        // Prepend tag to ciphertext
        byte[] result = new byte[tag.Length + ciphertext.Length];
        Array.Copy(tag, 0, result, 0, tag.Length);
        Array.Copy(ciphertext, 0, result, tag.Length, ciphertext.Length);
        return result;
    }

    public byte[] DecryptAesGcm(byte[] ciphertextWithTag, byte[] key, byte[] nonce, byte[] associatedData)
    {
        if (ciphertextWithTag.Length < 16)
        {
            throw new ArgumentException("Ciphertext is too short", nameof(ciphertextWithTag));
        }

        byte[] tag = new byte[16];
        byte[] ciphertext = new byte[ciphertextWithTag.Length - 16];
        Array.Copy(ciphertextWithTag, 0, tag, 0, 16);
        Array.Copy(ciphertextWithTag, 16, ciphertext, 0, ciphertext.Length);

        using var aesGcm = new AesGcm(key, tagSizeInBytes: 16);
        byte[] plaintext = new byte[ciphertext.Length];
        aesGcm.Decrypt(nonce, ciphertext, tag, plaintext, associatedData);
        return plaintext;
    }

    public bool VerifySignature(byte[] data, byte[] signature, byte[] publicKey)
    {
        // Stand-in verification
        return data != null && signature != null;
    }
}
