using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;

namespace Sayra.Client.OfflineQueue.Security;

public class QueueSecurityManager : IQueueSecurityManager
{
    private readonly ILogger<QueueSecurityManager> _logger;
    private readonly byte[] _aesKey;
    private readonly string _keyFilePath;
    private static readonly byte[] FallbackSalt = new byte[] { 0x14, 0xBE, 0x78, 0x3D, 0x9A, 0xE1, 0x6B, 0x25 };

    public QueueSecurityManager(ILogger<QueueSecurityManager> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        var dataDir = Path.Combine(AppContext.BaseDirectory, "Data");
        if (!Directory.Exists(dataDir))
        {
            Directory.CreateDirectory(dataDir);
        }

        _keyFilePath = Path.Combine(dataDir, "queue_key.bin");
        _aesKey = InitializeEncryptionKey();
    }

    private byte[] InitializeEncryptionKey()
    {
        try
        {
            if (File.Exists(_keyFilePath))
            {
                var protectedBytes = File.ReadAllBytes(_keyFilePath);
                return UnprotectKey(protectedBytes);
            }
            else
            {
                var rawKey = new byte[32]; // 256-bit key
                using (var rng = RandomNumberGenerator.Create())
                {
                    rng.GetBytes(rawKey);
                }

                var protectedBytes = ProtectKey(rawKey);
                File.WriteAllBytes(_keyFilePath, protectedBytes);
                _logger.LogInformation("Generated and saved new secure DPAPI-protected encryption key.");
                return rawKey;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize DPAPI encryption key. Falling back to cross-platform safe key.");
            return GenerateFallbackKey();
        }
    }

    private byte[] ProtectKey(byte[] rawKey)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            try
            {
                return ProtectedData.Protect(rawKey, null, DataProtectionScope.LocalMachine);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "DPAPI Protect failed under Windows. Using soft protection fallback.");
            }
        }

        // Non-Windows or DPAPI failure fallback (Simple XOR with static salt + machine hash for cross-platform support)
        return SoftProtect(rawKey);
    }

    private byte[] UnprotectKey(byte[] protectedKey)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            try
            {
                return ProtectedData.Unprotect(protectedKey, null, DataProtectionScope.LocalMachine);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "DPAPI Unprotect failed. Attempting soft unprotection fallback.");
            }
        }

        return SoftUnprotect(protectedKey);
    }

    private byte[] SoftProtect(byte[] data)
    {
        var result = new byte[data.Length];
        var xorMask = GenerateFallbackKey();
        for (int i = 0; i < data.Length; i++)
        {
            result[i] = (byte)(data[i] ^ xorMask[i % xorMask.Length]);
        }
        return result;
    }

    private byte[] SoftUnprotect(byte[] data)
    {
        return SoftProtect(data); // XOR is symmetric
    }

    private byte[] GenerateFallbackKey()
    {
        // Build a consistent fallback key based on Environment properties
        var entropy = Environment.MachineName + Environment.UserName + "SAYRA-OFFLINE-QUEUE-SALT-987123";
        using var sha = SHA256.Create();
        return sha.ComputeHash(Encoding.UTF8.GetBytes(entropy));
    }

    public string EncryptPayload(string plaintext)
    {
        if (plaintext == null) throw new ArgumentNullException(nameof(plaintext));

        using var aes = Aes.Create();
        aes.Key = _aesKey;
        aes.GenerateIV();

        using var encryptor = aes.CreateEncryptor();
        using var ms = new MemoryStream();

        // Write IV first
        ms.Write(aes.IV, 0, aes.IV.Length);

        using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
        using (var sw = new StreamWriter(cs, Encoding.UTF8))
        {
            sw.Write(plaintext);
        }

        return Convert.ToBase64String(ms.ToArray());
    }

    public string DecryptPayload(string ciphertext)
    {
        if (string.IsNullOrWhiteSpace(ciphertext)) throw new ArgumentException("Ciphertext cannot be null or empty.", nameof(ciphertext));

        var combinedBytes = Convert.FromBase64String(ciphertext);
        if (combinedBytes.Length < 16)
        {
            throw new InvalidOperationException("Invalid ciphertext payload length.");
        }

        using var aes = Aes.Create();
        aes.Key = _aesKey;

        var iv = new byte[16];
        Array.Copy(combinedBytes, 0, iv, 0, 16);
        aes.IV = iv;

        using var decryptor = aes.CreateDecryptor();
        using var ms = new MemoryStream(combinedBytes, 16, combinedBytes.Length - 16);
        using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
        using var sr = new StreamReader(cs, Encoding.UTF8);

        return sr.ReadToEnd();
    }

    public string GenerateSignature(string payload)
    {
        if (payload == null) throw new ArgumentNullException(nameof(payload));

        using var hmac = new HMACSHA256(_aesKey);
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        return Convert.ToBase64String(hash);
    }

    public bool VerifySignature(string payload, string signature)
    {
        if (payload == null) throw new ArgumentNullException(nameof(payload));
        if (string.IsNullOrWhiteSpace(signature)) return false;

        var expectedSignature = GenerateSignature(payload);
        return CryptographicEquals(signature, expectedSignature);
    }

    /// <summary>
    /// Constant-time comparison to prevent timing attacks.
    /// </summary>
    private static bool CryptographicEquals(string a, string b)
    {
        var aBytes = Encoding.UTF8.GetBytes(a);
        var bBytes = Encoding.UTF8.GetBytes(b);

        if (aBytes.Length != bBytes.Length) return false;

        int result = 0;
        for (int i = 0; i < aBytes.Length; i++)
        {
            result |= aBytes[i] ^ bBytes[i];
        }
        return result == 0;
    }
}
