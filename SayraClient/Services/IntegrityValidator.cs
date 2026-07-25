using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Sayra.Client.Shared.Interfaces.Security;

namespace SayraClient.Services;

public class IntegrityValidator : IIntegrityValidator
{
    private readonly ILogger<IntegrityValidator> _logger;
    private readonly SessionKeyManager _sessionKeyManager;
    private readonly TimeSpan _timestampTolerance = TimeSpan.FromSeconds(10);

    public IntegrityValidator(ILogger<IntegrityValidator> logger, SessionKeyManager sessionKeyManager)
    {
        _logger = logger;
        _sessionKeyManager = sessionKeyManager;
    }

    public string GenerateSignature(string data, DateTime timestamp)
    {
        byte[]? key = _sessionKeyManager.GetSessionKey();
        if (key == null) throw new InvalidOperationException("Session key not set.");

        string messageToSign = $"{timestamp:O}|{data}";
        using HMACSHA256 hmac = new(key);
        byte[] hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(messageToSign));
        return Convert.ToBase64String(hash);
    }

    public bool VerifySignature(string data, DateTime timestamp, string signature)
    {
        byte[]? key = _sessionKeyManager.GetSessionKey();
        if (key == null)
        {
            _logger.LogError("Verification failed: Session key not set.");
            return false;
        }

        // Check timestamp (Replay Protection)
        var now = DateTime.UtcNow;
        if (Math.Abs((now - timestamp.ToUniversalTime()).TotalSeconds) > _timestampTolerance.TotalSeconds)
        {
            _logger.LogWarning("Verification failed: Timestamp out of range. Received: {Received}, Now: {Now}", timestamp, now);
            return false;
        }

        string messageToSign = $"{timestamp:O}|{data}";
        using HMACSHA256 hmac = new(key);
        byte[] computedHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(messageToSign));
        string computedSignature = Convert.ToBase64String(computedHash);

        bool isValid = computedSignature == signature;
        if (!isValid)
        {
            _logger.LogWarning("Verification failed: Signature mismatch.");
        }

        return isValid;
    }

    public bool VerifyFileIntegrity(string filepath, string expectedHash)
    {
        try
        {
            if (!File.Exists(filepath))
            {
                _logger.LogError("Integrity check failed: File not found {Path}", filepath);
                return false;
            }

            using var sha256 = SHA256.Create();
            using var stream = File.OpenRead(filepath);
            byte[] hashBytes = sha256.ComputeHash(stream);
            string actualHash = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();

            bool isValid = actualHash == expectedHash.ToLowerInvariant();
            if (!isValid)
            {
                _logger.LogWarning("Integrity breach detected for {File}! Actual: {Actual}, Expected: {Expected}", filepath, actualHash, expectedHash);
            }
            return isValid;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying integrity of {File}", filepath);
            return false;
        }
    }

    // --- New IIntegrityValidator members ---

    public bool ValidateFile(string filePath, string expectedHash)
    {
        return VerifyFileIntegrity(filePath, expectedHash);
    }

    public bool ValidateProcess(int processId)
    {
        // Simple process verification check placeholder
        return true;
    }

    public bool VerifyIntegrity()
    {
        return true;
    }

    public bool VerifyAuthenticodeSignature(string filePath)
    {
        // Stand-in authenticode signature verification
        return File.Exists(filePath);
    }

    public string ComputeSha256Hash(string filePath)
    {
        if (!File.Exists(filePath)) return string.Empty;
        using var sha = SHA256.Create();
        using var stream = File.OpenRead(filePath);
        return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
    }

    public bool ValidateDllIntegrity(string dllName)
    {
        // Stand-in DLL check
        return true;
    }
}
