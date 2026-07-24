using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Sayra.Client.Configuration.Models;

namespace Sayra.Client.Configuration.Validation;

public class ConfigurationSignatureValidator
{
    private readonly string _publicKeyPath;
    private readonly ILogger<ConfigurationSignatureValidator>? _logger;

    public ConfigurationSignatureValidator(string? publicKeyPath = null, ILogger<ConfigurationSignatureValidator>? logger = null)
    {
        _publicKeyPath = publicKeyPath ?? Path.Combine(AppContext.BaseDirectory, "server_public.key");
        _logger = logger;
    }

    public virtual bool VerifySignature(ConfigurationPackage package)
    {
        if (package == null)
        {
            _logger?.LogWarning("Signature verification failed: Package is null.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(package.Signature))
        {
            _logger?.LogWarning("Signature verification failed: Signature is missing.");
            return false;
        }

        // Validate local payload hash to ensure it matches the hash field in the package (detect local modification)
        using (var sha256 = SHA256.Create())
        {
            byte[] payloadBytes = Encoding.UTF8.GetBytes(package.Payload);
            byte[] computedHashBytes = sha256.ComputeHash(payloadBytes);
            string computedHash = Convert.ToHexString(computedHashBytes).ToLowerInvariant();

            if (!string.Equals(computedHash, package.Hash?.ToLowerInvariant(), StringComparison.OrdinalIgnoreCase))
            {
                _logger?.LogWarning("Signature verification failed: Package payload hash does not match computed hash.");
                return false;
            }
        }

        try
        {
            string actualKeyPath = _publicKeyPath;
            if (!File.Exists(actualKeyPath))
            {
                // Fallback search for testing environments
                var altPaths = new[]
                {
                    Path.Combine(AppContext.BaseDirectory, "server_public.key"),
                    "server_public.key",
                    "../server_public.key",
                    "../../server_public.key"
                };

                foreach (var path in altPaths)
                {
                    if (File.Exists(path))
                    {
                        actualKeyPath = path;
                        break;
                    }
                }
            }

            if (!File.Exists(actualKeyPath))
            {
                _logger?.LogError($"Public key file not found at path: {_publicKeyPath}");
                return false;
            }

            string publicKeyPem = File.ReadAllText(actualKeyPath);
            using var rsa = RSA.Create();
            rsa.ImportFromPem(publicKeyPem);

            // Structure data to verify as a unified string
            string dataToVerify = $"{package.Version}{package.PayloadType}{package.Payload}{package.TargetClient}{package.TargetGroup}";
            byte[] dataBytes = Encoding.UTF8.GetBytes(dataToVerify);
            byte[] signatureBytes = Convert.FromBase64String(package.Signature);

            return rsa.VerifyData(dataBytes, signatureBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Cryptography error while verifying package RSA signature.");
            return false;
        }
    }
}
