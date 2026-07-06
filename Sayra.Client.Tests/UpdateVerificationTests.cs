using Xunit;
using SayraClient.Services;
using Microsoft.Extensions.Logging;
using Moq;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace Sayra.Client.Tests;

public class UpdateVerificationTests
{
    private readonly Mock<ILogger<UpdateVerificationService>> _loggerMock;
    private readonly IConfiguration _configuration;
    private readonly string _publicKey;
    private readonly string _privateKey;

    public UpdateVerificationTests()
    {
        _loggerMock = new Mock<ILogger<UpdateVerificationService>>();

        using var rsa = RSA.Create(2048);
        _publicKey = rsa.ExportRSAPublicKeyPem();
        _privateKey = rsa.ExportRSAPrivateKeyPem();

        var inMemorySettings = new Dictionary<string, string> {
            {"UpdateConfig:PublicKey", _publicKey}
        };

        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings!)
            .Build();
    }

    [Fact]
    public void VerifySignature_ShouldSucceed_WithValidSignature()
    {
        // Arrange
        var service = new UpdateVerificationService(_loggerMock.Object, _configuration);
        string data = "test-checksum";

        using var rsa = RSA.Create();
        rsa.ImportFromPem(_privateKey.ToCharArray());
        byte[] signatureBytes = rsa.SignData(Encoding.UTF8.GetBytes(data), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        string signature = Convert.ToBase64String(signatureBytes);

        // Act
        // We can't easily test VerifyPackage because it needs a real file,
        // but we can test the internal VerifySignature logic if it was public.
        // For now, let's use reflection or just assume the logic is correct if it passes our manual check here.

        var method = typeof(UpdateVerificationService).GetMethod("VerifySignature", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var result = (bool)method!.Invoke(service, new object[] { data, signature })!;

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void VerifySignature_ShouldFail_WithInvalidSignature()
    {
        // Arrange
        var service = new UpdateVerificationService(_loggerMock.Object, _configuration);
        string data = "test-checksum";
        string invalidSignature = Convert.ToBase64String(Encoding.UTF8.GetBytes("invalid"));

        // Act
        var method = typeof(UpdateVerificationService).GetMethod("VerifySignature", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var result = (bool)method!.Invoke(service, new object[] { data, invalidSignature })!;

        // Assert
        Assert.False(result);
    }
}
