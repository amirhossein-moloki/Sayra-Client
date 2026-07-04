using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SayraClient.Models;

namespace SayraClient.Services;

public class AuthManager
{
    private readonly ILogger<AuthManager> _logger;
    private readonly IConfiguration _configuration;
    private readonly SessionKeyManager _sessionKeyManager;
    private string? _currentChallenge;
    private byte[]? _pendingSessionKey;

    public AuthManager(ILogger<AuthManager> logger, IConfiguration configuration, SessionKeyManager sessionKeyManager)
    {
        _logger = logger;
        _configuration = configuration;
        _sessionKeyManager = sessionKeyManager;
    }

    public async Task<AuthResponseModel?> HandleChallengeAsync(AuthChallengeModel challenge)
    {
        _logger.LogInformation("Received auth challenge from server.");
        _currentChallenge = challenge.Challenge;

        string? masterKeyBase64 = _configuration["SAYRA_MASTER_KEY"] ?? _configuration["SecurityConfig:MasterKey"];
        if (string.IsNullOrEmpty(masterKeyBase64) || masterKeyBase64.Contains("PLACEHOLDER"))
        {
            _logger.LogError("MasterKey not configured correctly (missing SAYRA_MASTER_KEY env var or valid config value).");
            return null;
        }

        byte[] masterKey = Convert.FromBase64String(masterKeyBase64);

        // Generate a new session key
        byte[] sessionKey = new byte[32]; // AES-256
        RandomNumberGenerator.Fill(sessionKey);
        _pendingSessionKey = sessionKey;

        // HMAC the challenge with MasterKey to prove identity
        using HMACSHA256 hmac = new(masterKey);
        byte[] challengeBytes = Encoding.UTF8.GetBytes(_currentChallenge);
        byte[] responseHash = hmac.ComputeHash(challengeBytes);

        // Encrypt the session key with MasterKey
        using Aes aes = Aes.Create();
        aes.Key = masterKey;
        aes.GenerateIV();

        string encryptedSessionKey;
        using (var encryptor = aes.CreateEncryptor(aes.Key, aes.IV))
        using (var ms = new MemoryStream())
        {
            ms.Write(aes.IV, 0, aes.IV.Length);
            using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
            {
                cs.Write(sessionKey, 0, sessionKey.Length);
            }
            encryptedSessionKey = Convert.ToBase64String(ms.ToArray());
        }

        var response = new AuthResponseModel
        {
            Response = Convert.ToBase64String(responseHash),
            EncryptedSessionKey = encryptedSessionKey
        };

        _logger.LogInformation("Generated auth response with session key.");
        return response;
    }

    public void HandleAuthStatus(AuthStatusModel status)
    {
        if (status.Status == "SUCCESS" && _pendingSessionKey != null)
        {
            _logger.LogInformation("Authentication successful.");
            _sessionKeyManager.SetSessionKey(_pendingSessionKey);
            _pendingSessionKey = null;
        }
        else
        {
            _logger.LogWarning("Authentication failed: {Message}", status.Message);
            _sessionKeyManager.ClearSessionKey();
            _pendingSessionKey = null;
        }
    }

    public void Reset()
    {
        _pendingSessionKey = null;
        _currentChallenge = null;
    }
}
