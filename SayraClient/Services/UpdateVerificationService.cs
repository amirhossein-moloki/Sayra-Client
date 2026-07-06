using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;

namespace SayraClient.Services;

public class UpdateVerificationService
{
    private readonly ILogger<UpdateVerificationService> _logger;
    private readonly string _publicKeyPem;

    public UpdateVerificationService(ILogger<UpdateVerificationService> logger, Microsoft.Extensions.Configuration.IConfiguration configuration)
    {
        _logger = logger;
        // In production, the public key should be securely stored or embedded as a resource
        _publicKeyPem = configuration["UpdateConfig:PublicKey"] ?? "";
    }

    public bool VerifyPackage(string filePath, string expectedChecksum, string signature)
    {
        _logger.LogInformation("Verifying update package: {Path}", filePath);

        // 1. Verify SHA256 Checksum
        if (!VerifyChecksum(filePath, expectedChecksum))
        {
            _logger.LogError("SHA256 checksum mismatch for update package.");
            return false;
        }

        // 2. Verify RSA Signature
        if (string.IsNullOrEmpty(_publicKeyPem))
        {
            _logger.LogWarning("RSA Public Key not configured. Skipping signature verification (NOT RECOMMENDED FOR PRODUCTION).");
            return true;
        }

        return VerifySignature(expectedChecksum, signature);
    }

    private bool VerifyChecksum(string filePath, string expectedChecksum)
    {
        using var sha256 = SHA256.Create();
        using var stream = File.OpenRead(filePath);
        var hash = sha256.ComputeHash(stream);
        var hashString = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        return hashString.Equals(expectedChecksum, StringComparison.OrdinalIgnoreCase);
    }

    private bool VerifySignature(string data, string signature)
    {
        try
        {
            using var rsa = RSA.Create();
            rsa.ImportFromPem(_publicKeyPem.ToCharArray());

            byte[] dataBytes = Encoding.UTF8.GetBytes(data);
            byte[] signatureBytes = Convert.FromBase64String(signature);

            return rsa.VerifyData(dataBytes, signatureBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RSA signature verification failed.");
            return false;
        }
    }
}
