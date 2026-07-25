using System;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using SayraClient.Models;
using SayraClient.Services;
using Sayra.Client.Shared.Interfaces.Security;

namespace SayraClient.Security.Transport;

public class SecureTransportLayer
{
    private readonly ILogger<SecureTransportLayer> _logger;
    private readonly ICryptographyService _encryptionManager;
    private readonly IIntegrityValidator _integrityValidator;
    private readonly SessionKeyManager _sessionKeyManager;

    public SecureTransportLayer(
        ILogger<SecureTransportLayer> logger,
        ICryptographyService encryptionManager,
        IIntegrityValidator integrityValidator,
        SessionKeyManager sessionKeyManager)
    {
        _logger = logger;
        _encryptionManager = encryptionManager;
        _integrityValidator = integrityValidator;
        _sessionKeyManager = sessionKeyManager;
    }

    public string Wrap(object message)
    {
        if (!_sessionKeyManager.IsAuthenticated)
        {
             return JsonSerializer.Serialize(message);
        }

        string plaintext = JsonSerializer.Serialize(message);
        string encryptedPayload = _encryptionManager.Encrypt(plaintext);
        DateTime timestamp = DateTime.UtcNow;
        string signature = _integrityValidator.GenerateSignature(encryptedPayload, timestamp);

        var secureMessage = new SecureMessageModel
        {
            Payload = encryptedPayload,
            Signature = signature,
            Timestamp = timestamp
        };

        return JsonSerializer.Serialize(secureMessage);
    }

    public string? Unwrap(string json)
    {
        try
        {
            var secureMessage = JsonSerializer.Deserialize<SecureMessageModel>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (secureMessage != null && !string.IsNullOrEmpty(secureMessage.Payload))
            {
                if (!_integrityValidator.VerifySignature(secureMessage.Payload, secureMessage.Timestamp, secureMessage.Signature))
                {
                    _logger.LogWarning("Message integrity check failed.");
                    return null;
                }

                return _encryptionManager.Decrypt(secureMessage.Payload);
            }

            if (_sessionKeyManager.IsAuthenticated)
            {
                _logger.LogWarning("Received plaintext message while authenticated. Rejecting for security.");
                return null;
            }

            return json;
        }
        catch (Exception ex)
        {
            if (_sessionKeyManager.IsAuthenticated)
            {
                _logger.LogError(ex, "Failed to unwrap secure message while authenticated.");
                return null;
            }
            return json; // Fallback for handshake
        }
    }
}
