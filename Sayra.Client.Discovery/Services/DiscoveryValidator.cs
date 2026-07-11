using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Sayra.Client.Discovery.Models;

namespace Sayra.Client.Discovery.Services;

public class DiscoveryValidator
{
    private readonly ILogger<DiscoveryValidator> _logger;
    private readonly string _publicKeyPath;
    private readonly HashSet<string> _seenNonces = new();
    private readonly object _lock = new();

    public DiscoveryValidator(ILogger<DiscoveryValidator> logger, string publicKeyPath)
    {
        _logger = logger;
        _publicKeyPath = publicKeyPath;
    }

    public virtual bool Validate(ServerDiscoveryResponse response)
    {
        try
        {
            // 1. Message format (already partially checked by deserialization)
            if (response.type != "SAYRA_SERVER_RESPONSE")
            {
                _logger.LogWarning("Invalid response type: {type}", response.type);
                return false;
            }

            // 2. Timestamp (±10s window)
            if (!DateTimeOffset.TryParse(response.timestamp, out var serverTime))
            {
                _logger.LogWarning("Invalid timestamp format: {timestamp}", response.timestamp);
                return false;
            }

            if (Math.Abs((DateTimeOffset.UtcNow - serverTime).TotalSeconds) > 10)
            {
                _logger.LogWarning("Response rejected due to expired timestamp: {timestamp}", response.timestamp);
                return false;
            }

            // 3. Nonce uniqueness (using signature as unique identifier for the response is also an option, but nonce is requested)
            lock (_lock)
            {
                if (_seenNonces.Contains(response.nonce))
                {
                    _logger.LogWarning("Replay attack detected! Nonce already used: {nonce}", response.nonce);
                    return false;
                }
                _seenNonces.Add(response.nonce);

                // Keep cache small (FIFO approx)
                if (_seenNonces.Count > 1000)
                {
                    // Using a Queue for true FIFO would be better, but for this task keeping it simple.
                    _seenNonces.Remove(_seenNonces.First());
                }
            }

            // 4. Signature validity (RSA)
            if (!VerifySignature(response))
            {
                _logger.LogWarning("RSA Signature verification failed for server: {serverId}", response.serverId);
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating discovery response.");
            return false;
        }
    }

    private bool VerifySignature(ServerDiscoveryResponse response)
    {
        try
        {
            if (!File.Exists(_publicKeyPath))
            {
                _logger.LogError("Server public key not found at {path}", _publicKeyPath);
                return false;
            }

            string publicKeyPem = File.ReadAllText(_publicKeyPath);
            using var rsa = RSA.Create();
            rsa.ImportFromPem(publicKeyPem);

            string dataToVerify = $"{response.serverId}{response.serverName}{response.ip}{response.tcpPort}{response.timestamp}{response.nonce}";
            byte[] dataBytes = Encoding.UTF8.GetBytes(dataToVerify);
            byte[] signatureBytes = Convert.FromBase64String(response.signature);

            return rsa.VerifyData(dataBytes, signatureBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RSA verification error.");
            return false;
        }
    }
}
