using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;

namespace SayraClient.Services;

public class IntegrityValidator
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
}
