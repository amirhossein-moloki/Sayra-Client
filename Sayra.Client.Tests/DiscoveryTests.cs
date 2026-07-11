using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Moq;
using Sayra.Client.Discovery.Models;
using Sayra.Client.Discovery.Services;
using Xunit;

namespace Sayra.Client.Tests;

public class DiscoveryTests
{
    private readonly string _publicKeyPath = "test_server_public.key";
    private readonly string _privateKeyPath = "test_server_private.key";

    public DiscoveryTests()
    {
        using var rsa = RSA.Create();
        File.WriteAllText(_privateKeyPath, rsa.ExportRSAPrivateKeyPem());
        File.WriteAllText(_publicKeyPath, rsa.ExportRSAPublicKeyPem());
    }

    [Fact]
    public void DiscoveryValidator_ValidSignature_Accepted()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<DiscoveryValidator>>();
        var validator = new DiscoveryValidator(loggerMock.Object, _publicKeyPath);

        var response = new ServerDiscoveryResponse
        {
            serverId = "server-1",
            serverName = "Main Server",
            ip = "192.168.1.100",
            tcpPort = 5000,
            timestamp = DateTime.UtcNow.ToString("O"),
            nonce = Guid.NewGuid().ToString()
        };

        response.signature = SignResponse(response);

        // Act
        bool isValid = validator.Validate(response);

        // Assert
        Assert.True(isValid);
    }

    [Fact]
    public void DiscoveryValidator_InvalidSignature_Rejected()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<DiscoveryValidator>>();
        var validator = new DiscoveryValidator(loggerMock.Object, _publicKeyPath);

        var response = new ServerDiscoveryResponse
        {
            serverId = "server-1",
            serverName = "Main Server",
            ip = "192.168.1.100",
            tcpPort = 5000,
            timestamp = DateTime.UtcNow.ToString("O"),
            nonce = Guid.NewGuid().ToString(),
            signature = "InvalidSignature"
        };

        // Act
        bool isValid = validator.Validate(response);

        // Assert
        Assert.False(isValid);
    }

    [Fact]
    public void DiscoveryValidator_ReplayAttack_Rejected()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<DiscoveryValidator>>();
        var validator = new DiscoveryValidator(loggerMock.Object, _publicKeyPath);

        var response = new ServerDiscoveryResponse
        {
            serverId = "server-1",
            serverName = "Main Server",
            ip = "192.168.1.100",
            tcpPort = 5000,
            timestamp = DateTime.UtcNow.ToString("O"),
            nonce = "same-nonce"
        };
        response.signature = SignResponse(response);

        // Act
        bool firstResult = validator.Validate(response);
        bool secondResult = validator.Validate(response);

        // Assert
        Assert.True(firstResult);
        Assert.False(secondResult);
    }

    [Fact]
    public void DiscoveryValidator_ExpiredTimestamp_Rejected()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<DiscoveryValidator>>();
        var validator = new DiscoveryValidator(loggerMock.Object, _publicKeyPath);

        var response = new ServerDiscoveryResponse
        {
            serverId = "server-1",
            serverName = "Main Server",
            ip = "192.168.1.100",
            tcpPort = 5000,
            timestamp = DateTime.UtcNow.AddMinutes(-1).ToString("O"),
            nonce = Guid.NewGuid().ToString()
        };
        response.signature = SignResponse(response);

        // Act
        bool isValid = validator.Validate(response);

        // Assert
        Assert.False(isValid);
    }

    [Fact]
    public async Task DiscoveryManager_ServerSelection_PicksLowestLatency()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<DiscoveryManager>>();
        var configMock = new Mock<IConfiguration>();
        var udpClientMock = new Mock<UdpDiscoveryClient>(new Mock<ILogger<UdpDiscoveryClient>>().Object, 37020);
        var validatorMock = new Mock<DiscoveryValidator>(new Mock<ILogger<DiscoveryValidator>>().Object, _publicKeyPath);

        var responses = new List<ServerDiscoveryResponse>
        {
            new ServerDiscoveryResponse { serverId = "slow", Latency = 500 },
            new ServerDiscoveryResponse { serverId = "fast", Latency = 50 }
        };

        udpClientMock.Setup(x => x.BroadcastDiscoveryAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(responses);
        validatorMock.Setup(x => x.Validate(It.IsAny<ServerDiscoveryResponse>())).Returns(true);

        if (File.Exists("server_cache.json")) File.Delete("server_cache.json");
        var manager = new DiscoveryManager(loggerMock.Object, configMock.Object, udpClientMock.Object, validatorMock.Object);

        // Act
        var result = await manager.DiscoverAsync(CancellationToken.None, forceFresh: true);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("fast", result.serverId);
    }

    [Fact]
    public async Task DiscoveryManager_ServerSelection_PrioritizesTrustedServerOverLowestLatency()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<DiscoveryManager>>();
        var configMock = new Mock<IConfiguration>();
        var udpClientMock = new Mock<UdpDiscoveryClient>(new Mock<ILogger<UdpDiscoveryClient>>().Object, 37020);
        var validatorMock = new Mock<DiscoveryValidator>(new Mock<ILogger<DiscoveryValidator>>().Object, _publicKeyPath);

        var responses = new List<ServerDiscoveryResponse>
        {
            new ServerDiscoveryResponse { serverId = "other-server", Latency = 10, ip = "1.2.3.5", tcpPort = 5000 },
            new ServerDiscoveryResponse { serverId = "trusted-server", Latency = 100, ip = "1.2.3.4", tcpPort = 5000 }
        };

        udpClientMock.Setup(x => x.BroadcastDiscoveryAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(responses);
        validatorMock.Setup(x => x.Validate(It.IsAny<ServerDiscoveryResponse>())).Returns(true);

        // Pre-populate cache with "trusted-server"
        var cache = new ServerCache
        {
            ServerId = "trusted-server",
            ServerName = "Trusted Server",
            LastIPAddress = "1.2.3.4",
            TcpPort = 5000,
            LastConnected = DateTime.UtcNow
        };
        File.WriteAllText("server_cache.json", JsonSerializer.Serialize(cache));

        var manager = new DiscoveryManager(loggerMock.Object, configMock.Object, udpClientMock.Object, validatorMock.Object);

        // Act
        var result = await manager.DiscoverAsync(CancellationToken.None, forceFresh: true);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("trusted-server", result.serverId);

        if (File.Exists("server_cache.json")) File.Delete("server_cache.json");
    }

    [Fact]
    public async Task DiscoveryManager_CachePersistence_Works()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<DiscoveryManager>>();
        var configMock = new Mock<IConfiguration>();
        var udpClientMock = new Mock<UdpDiscoveryClient>(new Mock<ILogger<UdpDiscoveryClient>>().Object, 37020);
        var validatorMock = new Mock<DiscoveryValidator>(new Mock<ILogger<DiscoveryValidator>>().Object, _publicKeyPath);

        var response = new ServerDiscoveryResponse { serverId = "server-1", ip = "1.2.3.4", tcpPort = 5000 };

        udpClientMock.Setup(x => x.BroadcastDiscoveryAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(new List<ServerDiscoveryResponse> { response });
        validatorMock.Setup(x => x.Validate(It.IsAny<ServerDiscoveryResponse>())).Returns(true);

        if (File.Exists("server_cache.json")) File.Delete("server_cache.json");
        var manager = new DiscoveryManager(loggerMock.Object, configMock.Object, udpClientMock.Object, validatorMock.Object);

        // Act
        await manager.DiscoverAsync(CancellationToken.None, forceFresh: true);

        // New manager instance should pick up from cache
        var manager2 = new DiscoveryManager(loggerMock.Object, configMock.Object, udpClientMock.Object, validatorMock.Object);
        var cachedResult = await manager2.DiscoverAsync(CancellationToken.None, forceFresh: false);

        // Assert
        Assert.NotNull(cachedResult);
        Assert.Equal("server-1", cachedResult.serverId);
        udpClientMock.Verify(x => x.BroadcastDiscoveryAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()), Times.Once); // Only once from the first manager
    }

    private string SignResponse(ServerDiscoveryResponse response)
    {
        string privateKeyPem = File.ReadAllText(_privateKeyPath);
        using var rsa = RSA.Create();
        rsa.ImportFromPem(privateKeyPem);

        string dataToVerify = $"{response.serverId}{response.serverName}{response.ip}{response.tcpPort}{response.timestamp}{response.nonce}";
        byte[] dataBytes = Encoding.UTF8.GetBytes(dataToVerify);
        byte[] signatureBytes = rsa.SignData(dataBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        return Convert.ToBase64String(signatureBytes);
    }
}
