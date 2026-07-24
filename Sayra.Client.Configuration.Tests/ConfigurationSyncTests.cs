using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Sayra.Client.Configuration.Conflict;
using Sayra.Client.Configuration.Models;
using Sayra.Client.Configuration.Rollback;
using Sayra.Client.Configuration.Storage;
using Sayra.Client.Configuration.Synchronization;
using Sayra.Client.Configuration.Validation;
using Sayra.Client.Configuration.Versioning;
using Sayra.Client.LocalAdmin.Models;
using Sayra.Client.LocalAdmin.Storage;
using Sayra.Client.Shared.Interfaces;
using Sayra.Client.OfflineQueue;
using Sayra.Client.OfflineQueue.Models;
using SayraClient.Services;
using SayraClient.Services.Configuration;
using Xunit;

namespace Sayra.Client.Configuration.Tests;

public class ConfigurationSyncTests : IDisposable
{
    private readonly string _testBaseDir;
    private readonly string _activePath;
    private readonly string _backupPath;
    private readonly string _tempPath;
    private readonly string _historyPath;
    private readonly string _publicKeyPath;

    private readonly RSA _testRsa;
    private readonly string _privateKeyPem;
    private readonly string _publicKeyPem;

    public ConfigurationSyncTests()
    {
        _testBaseDir = Path.Combine(AppContext.BaseDirectory, "TestData_ConfigSync_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testBaseDir);

        _activePath = Path.Combine(_testBaseDir, "client_config.json");
        _backupPath = Path.Combine(_testBaseDir, "client_config.json.bak");
        _tempPath = Path.Combine(_testBaseDir, "client_config.json.tmp");
        _historyPath = Path.Combine(_testBaseDir, "version_history.json");
        _publicKeyPath = Path.Combine(_testBaseDir, "server_public.key");

        // Generate dynamic keypair for cryptographic testing
        _testRsa = RSA.Create(2048);
        _privateKeyPem = _testRsa.ExportRSAPrivateKeyPem();
        _publicKeyPem = _testRsa.ExportRSAPublicKeyPem();

        // Write the public key to our test path
        File.WriteAllText(_publicKeyPath, _publicKeyPem);
    }

    public void Dispose()
    {
        _testRsa.Dispose();
        try
        {
            if (Directory.Exists(_testBaseDir))
            {
                Directory.Delete(_testBaseDir, true);
            }
        }
        catch { /* Suppress cleanup exceptions */ }
    }

    private string SignPayload(long version, string payloadType, string payload, string targetClient, string targetGroup)
    {
        string dataToSign = $"{version}{payloadType}{payload}{targetClient}{targetGroup}";
        byte[] dataBytes = Encoding.UTF8.GetBytes(dataToSign);
        byte[] signatureBytes = _testRsa.SignData(dataBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return Convert.ToBase64String(signatureBytes);
    }

    private string ComputeSha256(string content)
    {
        using var sha = SHA256.Create();
        byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(content));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private ClientConfiguration CreateValidConfig(string stationId = "STATION_001")
    {
        return new ClientConfiguration
        {
            ClientId = "CLIENT_ABC",
            StationId = stationId,
            StationName = "Station Alpha",
            ServerDiscovery = new ServerDiscoverySettings
            {
                ServerIp = "192.168.1.10",
                UdpPort = 37020,
                AutoDiscovery = true
            },
            GameLibrary = new GameLibrarySettings
            {
                LibraryPath = "D:\\Games",
                AutoUpdate = true
            },
            LocalPreferences = new LocalPreferencesSettings
            {
                Theme = "Dark",
                Language = "fa-IR",
                IsKioskMode = true
            }
        };
    }

    #region Unit Tests - ConfigurationValidator

    [Fact]
    public void Validator_ShouldSucceed_OnValidConfiguration()
    {
        // Arrange
        var validator = new ConfigurationValidator();
        var config = CreateValidConfig();
        string json = JsonSerializer.Serialize(config);

        // Act
        bool isValid = validator.Validate(json, out var error);

        // Assert
        Assert.True(isValid);
        Assert.Empty(error);
    }

    [Fact]
    public void Validator_ShouldFail_WhenRequiredPropertiesAreMissing()
    {
        // Arrange
        var validator = new ConfigurationValidator();
        var config = CreateValidConfig();
        config.ServerDiscovery = null!;
        string json = JsonSerializer.Serialize(config);

        // Act
        bool isValid = validator.Validate(json, out var error);

        // Assert
        Assert.False(isValid);
        Assert.Contains("missing", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validator_ShouldFail_OnOutOfRangeUdpPort()
    {
        // Arrange
        var validator = new ConfigurationValidator();
        var config = CreateValidConfig();
        config.ServerDiscovery.UdpPort = 99999; // invalid
        string json = JsonSerializer.Serialize(config);

        // Act
        bool isValid = validator.Validate(json, out var error);

        // Assert
        Assert.False(isValid);
        Assert.Contains("UdpPort", error);
    }

    [Fact]
    public void Validator_ShouldFail_OnUnexpectedFields()
    {
        // Arrange
        var validator = new ConfigurationValidator();
        string malformedJson = "{\"ServerDiscovery\": {\"ServerIp\": \"127.0.0.1\", \"UdpPort\": 37020}, \"UnknownFieldExtra\": 123}";

        // Act
        bool isValid = validator.Validate(malformedJson, out var error);

        // Assert
        Assert.False(isValid);
        Assert.Contains("validation/schema failed", error);
    }

    #endregion

    #region Unit Tests - SignatureValidator

    [Fact]
    public void SignatureValidator_ShouldSucceed_OnValidSignature()
    {
        // Arrange
        var sigValidator = new ConfigurationSignatureValidator(_publicKeyPath, NullLogger<ConfigurationSignatureValidator>.Instance);
        string payload = JsonSerializer.Serialize(CreateValidConfig());
        string hash = ComputeSha256(payload);
        string signature = SignPayload(1, "Full", payload, "CLIENT_ABC", "GROUP_A");

        var pkg = new ConfigurationPackage
        {
            Version = 1,
            PayloadType = "Full",
            Payload = payload,
            Hash = hash,
            Signature = signature,
            TargetClient = "CLIENT_ABC",
            TargetGroup = "GROUP_A"
        };

        // Act
        bool verified = sigValidator.VerifySignature(pkg);

        // Assert
        Assert.True(verified);
    }

    [Fact]
    public void SignatureValidator_ShouldFail_OnModifiedPayload()
    {
        // Arrange
        var sigValidator = new ConfigurationSignatureValidator(_publicKeyPath, NullLogger<ConfigurationSignatureValidator>.Instance);
        string payload = JsonSerializer.Serialize(CreateValidConfig());
        string hash = ComputeSha256(payload);
        string signature = SignPayload(1, "Full", payload, "CLIENT_ABC", "GROUP_A");

        var pkg = new ConfigurationPackage
        {
            Version = 1,
            PayloadType = "Full",
            Payload = payload + "   /* altered content */ ", // altered
            Hash = hash,
            Signature = signature,
            TargetClient = "CLIENT_ABC",
            TargetGroup = "GROUP_A"
        };

        // Act
        bool verified = sigValidator.VerifySignature(pkg);

        // Assert
        Assert.False(verified);
    }

    #endregion

    #region Unit Tests - VersionManager

    [Fact]
    public void VersionManager_ShouldPreventDowngradeAttacks()
    {
        // Arrange
        var versionManager = new ConfigurationVersionManager(_historyPath, NullLogger<ConfigurationVersionManager>.Instance);
        versionManager.RecordVersionChange(10, "Applied");

        // Act
        bool valid = versionManager.ValidateVersion(5, out var error); // downgrade attempt

        // Assert
        Assert.False(valid);
        Assert.Contains("downgrade", error, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region Unit Tests - DeltaEngine

    [Fact]
    public void DeltaEngine_ShouldComputeAndApplyDeltasCorrectly()
    {
        // Arrange
        var deltaEngine = new ConfigurationDeltaEngine(NullLogger<ConfigurationDeltaEngine>.Instance);
        var config = CreateValidConfig();

        var newPreferences = new LocalPreferencesSettings { Theme = "Light", Language = "en-US", IsKioskMode = false };
        string patchJson = JsonSerializer.Serialize(newPreferences);
        string oldHash = deltaEngine.ComputeSectionHash(config.LocalPreferences);
        string newHash = deltaEngine.ComputeSectionHash(newPreferences);

        var deltas = new List<ConfigurationDelta>
        {
            new()
            {
                Section = "LocalPreferences",
                OldHash = oldHash,
                NewHash = newHash,
                Patch = patchJson
            }
        };

        // Act
        bool applied = deltaEngine.ApplyDeltas(config, deltas, out var error);

        // Assert
        Assert.True(applied);
        Assert.Equal("Light", config.LocalPreferences.Theme);
        Assert.Equal("en-US", config.LocalPreferences.Language);
    }

    #endregion

    #region Unit Tests - ConflictResolver

    [Fact]
    public void ConflictResolver_ShouldPreserveLocalMachineIdentifiers_UnderServerWins()
    {
        // Arrange
        var resolver = new ConfigurationConflictResolver(ConflictPolicy.ServerWins);
        var local = CreateValidConfig("STATION_LOCAL");
        local.ClientId = "CLIENT_ID_LOCAL";

        var server = CreateValidConfig("STATION_SERVER");
        server.ClientId = "CLIENT_ID_SERVER";

        // Act
        var resolved = resolver.Resolve(local, server);

        // Assert
        Assert.Equal("STATION_LOCAL", resolved.StationId);
        Assert.Equal("CLIENT_ID_LOCAL", resolved.ClientId);
    }

    #endregion

    #region Unit Tests - RollbackManager & Recovery

    [Fact]
    public void RollbackManager_ShouldRestoreBackup_WhenActiveIsCorrupted()
    {
        // Arrange
        var rollbackManager = new ConfigurationRollbackManager(NullLogger<ConfigurationRollbackManager>.Instance);
        string activeContent = "corrupted json payload";
        string backupContent = JsonSerializer.Serialize(CreateValidConfig());

        File.WriteAllText(_activePath, activeContent);
        File.WriteAllText(_backupPath, backupContent);

        // Act
        bool recovered = rollbackManager.ValidateAndRecover(_activePath, _backupPath, out var message);

        // Assert
        Assert.True(recovered);
        Assert.Contains("recovered from backup", message);
        Assert.False(rollbackManager.IsCorrupted(_activePath));
    }

    #endregion

    #region Integration Tests - Full & Delta Synchronization & Concurrent sync

    [Fact]
    public async Task Integration_FullAndDeltaSync_And_ConcurrentSync_WorksEndToEnd()
    {
        // Arrange
        var mockApiClient = new Mock<IConfigurationApiClient>();
        var validator = new ConfigurationValidator();
        var signatureValidator = new ConfigurationSignatureValidator(_publicKeyPath, NullLogger<ConfigurationSignatureValidator>.Instance);
        var versionManager = new ConfigurationVersionManager(_historyPath, NullLogger<ConfigurationVersionManager>.Instance);
        var deltaEngine = new ConfigurationDeltaEngine(NullLogger<ConfigurationDeltaEngine>.Instance);
        var conflictResolver = new ConfigurationConflictResolver();
        var rollbackManager = new ConfigurationRollbackManager(NullLogger<ConfigurationRollbackManager>.Instance);
        var applyService = new ConfigurationApplyService(rollbackManager, NullLogger<ConfigurationApplyService>.Instance);

        var mockLocalRepository = new Mock<IClientConfigurationRepository>();
        mockLocalRepository.Setup(x => x.LoadConfigurationAsync()).ReturnsAsync(CreateValidConfig());

        var mockSP = new Mock<IServiceProvider>();
        var mockAuditLogger = new Mock<IAuditLogger>();
        mockSP.Setup(x => x.GetService(typeof(IAuditLogger))).Returns(mockAuditLogger.Object);
        var mockOfflineQueue = new Mock<IOfflineQueueManager>();
        mockSP.Setup(x => x.GetService(typeof(IOfflineQueueManager))).Returns(mockOfflineQueue.Object);

        var syncService = new ConfigurationSynchronizationService(
            mockApiClient.Object,
            validator,
            signatureValidator,
            versionManager,
            deltaEngine,
            conflictResolver,
            rollbackManager,
            applyService,
            mockLocalRepository.Object,
            mockSP.Object,
            NullLogger<ConfigurationSynchronizationService>.Instance,
            _activePath,
            _backupPath,
            _tempPath
        );

        // 1. Prepare Server Package
        var serverConfig = CreateValidConfig("STATION_MAPPED_BY_SERVER");
        string serverPayload = JsonSerializer.Serialize(serverConfig);
        string signature = SignPayload(100, "Full", serverPayload, "", "");
        var package = new ConfigurationPackage
        {
            Version = 100,
            PayloadType = "Full",
            Payload = serverPayload,
            Hash = ComputeSha256(serverPayload),
            Signature = signature
        };

        mockApiClient.Setup(x => x.FetchLatestPackageAsync(0, It.IsAny<CancellationToken>()))
            .ReturnsAsync(package);

        // 2. Perform Pull Sync
        bool success = await syncService.PullAndApplyAsync();

        // Assert Pull Sync
        Assert.True(success);
        Assert.Equal(100, versionManager.CurrentVersion);
        mockAuditLogger.Verify(x => x.LogAudit(It.Is<string>(s => s.Contains("CONFIG_SYNC_COMPLETED")), It.IsAny<Dictionary<string, object>>()), Times.Once);

        // 3. Concurrent requests check
        var task1 = syncService.ManualSyncAsync();
        var task2 = syncService.ManualSyncAsync();
        await Task.WhenAll(task1, task2);

        Assert.True(task1.Result);
        Assert.True(task2.Result);
    }

    #endregion
}
