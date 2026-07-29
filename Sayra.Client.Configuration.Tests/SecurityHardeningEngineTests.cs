using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using SayraClient.Services.Recovery;
using Sayra.Client.Shared.Interfaces;
using Sayra.Client.Shared.Interfaces.Recovery;
using Sayra.Client.Shared.Interfaces.Security;
using Sayra.Client.Shared.Models;
using Sayra.Client.Shared.Models.Recovery;
using Sayra.Client.Shared.Models.Recovery.Events;
using Sayra.Client.Shared.UpdatePlatform.Application.Interfaces;
using Xunit;

namespace Sayra.Client.Configuration.Tests
{
    [Collection("Stage7Tests")]
    public class SecurityHardeningEngineTests
    {
        private readonly Mock<ILogger<SecurityHardeningService>> _loggerMock = new();
        private readonly Mock<IEventDispatcher> _eventDispatcherMock = new();
        private readonly Mock<ILocalDatabaseService> _dbServiceMock = new();
        private readonly Mock<IPolicyRepository> _policyRepoMock = new();
        private readonly Mock<IAdvertisementRepository> _adRepoMock = new();
        private readonly Mock<Sayra.Client.Shared.Interfaces.Security.ISignatureVerifier> _sigVerifierMock = new();
        private readonly Mock<IPackageVerifier> _packageVerifierMock = new();
        private readonly Mock<IAuthenticodeVerifier> _authenticodeVerifierMock = new();
        private readonly ServiceCollection _services;
        private readonly IServiceProvider _serviceProvider;

        public SecurityHardeningEngineTests()
        {
            _services = new ServiceCollection();
            _services.AddLogging();

            _services.AddSingleton(_loggerMock.Object);
            _services.AddSingleton(_eventDispatcherMock.Object);
            _services.AddSingleton(_dbServiceMock.Object);
            _services.AddSingleton(_policyRepoMock.Object);
            _services.AddSingleton(_adRepoMock.Object);
            _services.AddSingleton(_sigVerifierMock.Object);
            _services.AddSingleton(_packageVerifierMock.Object);
            _services.AddSingleton(_authenticodeVerifierMock.Object);

            _serviceProvider = _services.BuildServiceProvider();
        }

        #region Configuration Validation Tests

        [Fact]
        public async Task ValidateConfiguration_ShouldPass_WhenConfigFileExistsAndIsValid()
        {
            // Arrange
            string configPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
            string sigPath = configPath + ".sig";

            var dummyConfig = new { Version = "1.0.0", ConfigVersion = "1.0.0", Setting = "Test" };
            await File.WriteAllTextAsync(configPath, JsonSerializer.Serialize(dummyConfig));
            await File.WriteAllTextAsync(sigPath, "valid-signature");

            string publicKeyPath = Path.Combine(AppContext.BaseDirectory, "server_public.key");
            await File.WriteAllTextAsync(publicKeyPath, "public-key-content");

            _sigVerifierMock.Setup(s => s.VerifySignature(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(true);

            var service = new SecurityHardeningService(_loggerMock.Object, _serviceProvider, _eventDispatcherMock.Object);

            // Act
            var result = await service.ValidateConfigurationAsync(CancellationToken.None);

            // Assert
            Assert.Equal(SecurityValidationState.Passed, result.ValidationState);
            Assert.Equal("Configuration", result.TargetName);

            _eventDispatcherMock.Verify(d => d.Dispatch(It.IsAny<SecurityValidationStartedEvent>()), Times.Once);
            _eventDispatcherMock.Verify(d => d.Dispatch(It.IsAny<SecurityValidationCompletedEvent>()), Times.Once);

            // Cleanup
            if (File.Exists(configPath)) File.Delete(configPath);
            if (File.Exists(sigPath)) File.Delete(sigPath);
            if (File.Exists(publicKeyPath)) File.Delete(publicKeyPath);
        }

        [Fact]
        public async Task ValidateConfiguration_ShouldFail_WhenConfigFileIsMissing()
        {
            // Arrange
            string configPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
            if (File.Exists(configPath)) File.Delete(configPath);

            var service = new SecurityHardeningService(_loggerMock.Object, _serviceProvider, _eventDispatcherMock.Object);

            // Act
            var result = await service.ValidateConfigurationAsync(CancellationToken.None);

            // Assert
            Assert.Equal(SecurityValidationState.Failed, result.ValidationState);
            _eventDispatcherMock.Verify(d => d.Dispatch(It.IsAny<SecurityValidationFailedEvent>()), Times.Once);
        }

        [Fact]
        public async Task ValidateConfiguration_ShouldDetectTampering_WhenSignatureIsInvalid()
        {
            // Arrange
            string configPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
            string sigPath = configPath + ".sig";

            var dummyConfig = new { Version = "1.0.0", ConfigVersion = "1.0.0", Setting = "Test" };
            await File.WriteAllTextAsync(configPath, JsonSerializer.Serialize(dummyConfig));
            await File.WriteAllTextAsync(sigPath, "tampered-signature");

            string publicKeyPath = Path.Combine(AppContext.BaseDirectory, "server_public.key");
            await File.WriteAllTextAsync(publicKeyPath, "public-key-content");

            _sigVerifierMock.Setup(s => s.VerifySignature(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(false); // Signature validation failure!

            var service = new SecurityHardeningService(_loggerMock.Object, _serviceProvider, _eventDispatcherMock.Object);

            // Act
            var result = await service.ValidateConfigurationAsync(CancellationToken.None);

            // Assert
            Assert.Equal(SecurityValidationState.Tampered, result.ValidationState);
            _eventDispatcherMock.Verify(d => d.Dispatch(It.IsAny<SignatureValidationFailedEvent>()), Times.Once);
            _eventDispatcherMock.Verify(d => d.Dispatch(It.IsAny<TamperDetectedEvent>()), Times.Once);

            // Cleanup
            if (File.Exists(configPath)) File.Delete(configPath);
            if (File.Exists(sigPath)) File.Delete(sigPath);
            if (File.Exists(publicKeyPath)) File.Delete(publicKeyPath);
        }

        #endregion

        #region Policy Validation Tests

        [Fact]
        public async Task ValidatePolicy_ShouldPass_WithValidPolicies()
        {
            // Arrange
            var dummyPolicies = new List<PolicyProfile>
            {
                new PolicyProfile
                {
                    PolicyId = "POL-1",
                    Version = 1,
                    Signature = "policy-sig",
                    Rules = new List<PolicyRule>
                    {
                        new PolicyRule { RuleId = "RULE-1", Action = "BLOCK_USB", Value = "True" }
                    }
                }
            };

            _policyRepoMock.Setup(r => r.GetActivePoliciesAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(dummyPolicies);

            string publicKeyPath = Path.Combine(AppContext.BaseDirectory, "server_public.key");
            await File.WriteAllTextAsync(publicKeyPath, "public-key-content");

            _sigVerifierMock.Setup(s => s.VerifySignature(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(true);

            var service = new SecurityHardeningService(_loggerMock.Object, _serviceProvider, _eventDispatcherMock.Object);

            // Act
            var result = await service.ValidatePolicyAsync(CancellationToken.None);

            // Assert
            Assert.Equal(SecurityValidationState.Passed, result.ValidationState);
            _eventDispatcherMock.Verify(d => d.Dispatch(It.IsAny<SecurityValidationCompletedEvent>()), Times.Once);

            // Cleanup
            if (File.Exists(publicKeyPath)) File.Delete(publicKeyPath);
        }

        [Fact]
        public async Task ValidatePolicy_ShouldDetectTampering_WithInvalidSignature()
        {
            // Arrange
            var dummyPolicies = new List<PolicyProfile>
            {
                new PolicyProfile
                {
                    PolicyId = "POL-1",
                    Version = 1,
                    Signature = "policy-sig",
                    Rules = new List<PolicyRule>
                    {
                        new PolicyRule { RuleId = "RULE-1", Action = "BLOCK_USB", Value = "True" }
                    }
                }
            };

            _policyRepoMock.Setup(r => r.GetActivePoliciesAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(dummyPolicies);

            string publicKeyPath = Path.Combine(AppContext.BaseDirectory, "server_public.key");
            await File.WriteAllTextAsync(publicKeyPath, "public-key-content");

            _sigVerifierMock.Setup(s => s.VerifySignature(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(false); // Tampered signature!

            var service = new SecurityHardeningService(_loggerMock.Object, _serviceProvider, _eventDispatcherMock.Object);

            // Act
            var result = await service.ValidatePolicyAsync(CancellationToken.None);

            // Assert
            Assert.Equal(SecurityValidationState.Tampered, result.ValidationState);
            _eventDispatcherMock.Verify(d => d.Dispatch(It.IsAny<TamperDetectedEvent>()), Times.Once);

            // Cleanup
            if (File.Exists(publicKeyPath)) File.Delete(publicKeyPath);
        }

        #endregion

        #region Database Validation Tests

        [Fact]
        public async Task ValidateDatabase_ShouldPass_WithSoundDatabase()
        {
            // Arrange
            using var sqliteConn = new SqliteConnection("Data Source=:memory:;");
            await sqliteConn.OpenAsync();

            using (var setupCmd = sqliteConn.CreateCommand())
            {
                setupCmd.CommandText = "PRAGMA user_version = 5;";
                await setupCmd.ExecuteNonQueryAsync();
            }

            _dbServiceMock.Setup(d => d.CreateConnection()).Returns(sqliteConn);

            var service = new SecurityHardeningService(_loggerMock.Object, _serviceProvider, _eventDispatcherMock.Object);

            // Act
            var result = await service.ValidateDatabaseAsync(CancellationToken.None);

            // Assert
            Assert.Equal(SecurityValidationState.Passed, result.ValidationState);
            Assert.Contains("User Version: 5", result.Message);
            _eventDispatcherMock.Verify(d => d.Dispatch(It.IsAny<SecurityValidationCompletedEvent>()), Times.Once);
        }

        [Fact]
        public async Task ValidateDatabase_ShouldDetectCorruption_WhenConnectionThrows()
        {
            // Arrange
            _dbServiceMock.Setup(d => d.CreateConnection())
                .Throws(new SqliteException("database is locked or corrupted", 26));

            var service = new SecurityHardeningService(_loggerMock.Object, _serviceProvider, _eventDispatcherMock.Object);

            // Act
            var result = await service.ValidateDatabaseAsync(CancellationToken.None);

            // Assert
            Assert.Equal(SecurityValidationState.Failed, result.ValidationState);
            _eventDispatcherMock.Verify(d => d.Dispatch(It.IsAny<SecurityValidationFailedEvent>()), Times.Once);
        }

        #endregion

        #region Media Validation Tests

        [Fact]
        public async Task ValidateMedia_ShouldPass_WhenAllMediaAreIntact()
        {
            // Arrange
            string mediaFile = Path.Combine(AppContext.BaseDirectory, "media_test.jpg");
            await File.WriteAllTextAsync(mediaFile, "Test Ad Media");

            using var sha = System.Security.Cryptography.SHA256.Create();
            byte[] hashBytes = sha.ComputeHash(Encoding.UTF8.GetBytes("Test Ad Media"));
            string expectedHash = Convert.ToHexString(hashBytes).ToLower();

            var dummyMedia = new List<DownloadedMedia>
            {
                new DownloadedMedia
                {
                    CampaignId = "CAMP-1",
                    MediaPath = mediaFile,
                    Checksum = expectedHash
                }
            };

            _adRepoMock.Setup(r => r.GetDownloadedMediaListAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(dummyMedia);

            var service = new SecurityHardeningService(_loggerMock.Object, _serviceProvider, _eventDispatcherMock.Object);

            // Act
            var result = await service.ValidateMediaAsync(CancellationToken.None);

            // Assert
            Assert.Equal(SecurityValidationState.Passed, result.ValidationState);

            // Cleanup
            if (File.Exists(mediaFile)) File.Delete(mediaFile);
        }

        [Fact]
        public async Task ValidateMedia_ShouldDetectTampering_WhenMediaChecksumMismatch()
        {
            // Arrange
            string mediaFile = Path.Combine(AppContext.BaseDirectory, "media_test.jpg");
            await File.WriteAllTextAsync(mediaFile, "Test Ad Media");

            var dummyMedia = new List<DownloadedMedia>
            {
                new DownloadedMedia
                {
                    CampaignId = "CAMP-1",
                    MediaPath = mediaFile,
                    Checksum = "invalid-expected-hash" // Trigger tamper check!
                }
            };

            _adRepoMock.Setup(r => r.GetDownloadedMediaListAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(dummyMedia);

            var service = new SecurityHardeningService(_loggerMock.Object, _serviceProvider, _eventDispatcherMock.Object);

            // Act
            var result = await service.ValidateMediaAsync(CancellationToken.None);

            // Assert
            Assert.Equal(SecurityValidationState.Tampered, result.ValidationState);
            _eventDispatcherMock.Verify(d => d.Dispatch(It.IsAny<IntegrityViolationDetectedEvent>()), Times.Once);
            _eventDispatcherMock.Verify(d => d.Dispatch(It.IsAny<TamperDetectedEvent>()), Times.Once);

            // Cleanup
            if (File.Exists(mediaFile)) File.Delete(mediaFile);
        }

        #endregion

        #region Plugin Validation Tests

        [Fact]
        public async Task ValidatePlugins_ShouldPass_WithEmptyPluginsDirectory()
        {
            // Arrange
            string pluginsDir = Path.Combine(AppContext.BaseDirectory, "plugins");
            if (Directory.Exists(pluginsDir)) Directory.Delete(pluginsDir, true);

            var service = new SecurityHardeningService(_loggerMock.Object, _serviceProvider, _eventDispatcherMock.Object);

            // Act
            var result = await service.ValidatePluginsAsync(CancellationToken.None);

            // Assert
            Assert.Equal(SecurityValidationState.Passed, result.ValidationState);
        }

        [Fact]
        public async Task ValidatePlugins_ShouldValidateManifestAndAssemblies_WhenDirectoryNotEmpty()
        {
            // Arrange
            string pluginsDir = Path.Combine(AppContext.BaseDirectory, "plugins");
            string testPluginDir = Path.Combine(pluginsDir, "TestPlugin");
            Directory.CreateDirectory(testPluginDir);

            string manifestPath = Path.Combine(testPluginDir, "plugin.json");
            var manifestObj = new { Id = "Plugin-1", Version = "1.0.0", EntryPoint = "Plugin.dll" };
            await File.WriteAllTextAsync(manifestPath, JsonSerializer.Serialize(manifestObj));

            string assemblyPath = Path.Combine(testPluginDir, "Plugin.dll");
            await File.WriteAllTextAsync(assemblyPath, "dummy assembly binary");

            _authenticodeVerifierMock.Setup(v => v.VerifyFileAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Sayra.Client.Shared.UpdatePlatform.Domain.Models.SecurityValidationResult.Successful("Trusted Publisher", "THUMBPRINT"));

            var service = new SecurityHardeningService(_loggerMock.Object, _serviceProvider, _eventDispatcherMock.Object);

            // Act
            var result = await service.ValidatePluginsAsync(CancellationToken.None);

            // Assert
            Assert.Equal(SecurityValidationState.Passed, result.ValidationState);

            // Cleanup
            if (Directory.Exists(pluginsDir)) Directory.Delete(pluginsDir, true);
        }

        #endregion

        #region Package Validation Tests

        [Fact]
        public async Task ValidatePackages_ShouldPass_WhenNoPackagesExist()
        {
            // Arrange
            string packagesDir = Path.Combine(AppContext.BaseDirectory, "packages");
            if (Directory.Exists(packagesDir)) Directory.Delete(packagesDir, true);

            var service = new SecurityHardeningService(_loggerMock.Object, _serviceProvider, _eventDispatcherMock.Object);

            // Act
            var result = await service.ValidatePackagesAsync(CancellationToken.None);

            // Assert
            Assert.Equal(SecurityValidationState.Passed, result.ValidationState);
        }

        [Fact]
        public async Task ValidatePackages_ShouldVerifyPackageSignature_WhenPackagesExist()
        {
            // Arrange
            string packagesDir = Path.Combine(AppContext.BaseDirectory, "packages");
            Directory.CreateDirectory(packagesDir);

            string spkPath = Path.Combine(packagesDir, "update-1.spk");
            await File.WriteAllTextAsync(spkPath, "spk package payload");

            string sigPath = spkPath + ".sig";
            await File.WriteAllTextAsync(sigPath, "expected-sig-content");

            _packageVerifierMock.Setup(v => v.VerifyFileSignatureAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            var service = new SecurityHardeningService(_loggerMock.Object, _serviceProvider, _eventDispatcherMock.Object);

            // Act
            var result = await service.ValidatePackagesAsync(CancellationToken.None);

            // Assert
            Assert.Equal(SecurityValidationState.Passed, result.ValidationState);

            // Cleanup
            if (Directory.Exists(packagesDir)) Directory.Delete(packagesDir, true);
        }

        #endregion

        #region Executable Validation Tests

        [Fact]
        public async Task ValidateExecutable_ShouldPass_AndGetExecutableMetadata()
        {
            // Arrange
            _authenticodeVerifierMock.Setup(v => v.VerifyFileAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Sayra.Client.Shared.UpdatePlatform.Domain.Models.SecurityValidationResult.Successful("SAYRA Enterprise", "THUMBPRINT"));

            var service = new SecurityHardeningService(_loggerMock.Object, _serviceProvider, _eventDispatcherMock.Object);

            // Act
            var result = await service.ValidateExecutableAsync(CancellationToken.None);

            // Assert
            Assert.Equal(SecurityValidationState.Passed, result.ValidationState);
            Assert.NotNull(result.ComputedSignature);
        }

        #endregion

        #region Concurrent validation & Cancellation Tests

        [Fact]
        public async Task RunFullValidation_ShouldExecuteAllChecksInParallel_AndReturnOverallState()
        {
            // Arrange
            string configPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
            var dummyConfig = new { Version = "1.0.0", ConfigVersion = "1.0.0", Setting = "Test" };
            await File.WriteAllTextAsync(configPath, JsonSerializer.Serialize(dummyConfig));

            _authenticodeVerifierMock.Setup(v => v.VerifyFileAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Sayra.Client.Shared.UpdatePlatform.Domain.Models.SecurityValidationResult.Successful("SAYRA", "THUMB"));

            using var sqliteConn = new SqliteConnection("Data Source=:memory:;");
            await sqliteConn.OpenAsync();
            _dbServiceMock.Setup(d => d.CreateConnection()).Returns(sqliteConn);

            var service = new SecurityHardeningService(_loggerMock.Object, _serviceProvider, _eventDispatcherMock.Object);

            // Act
            var results = await service.RunFullValidationAsync(CancellationToken.None);

            // Assert
            Assert.NotEmpty(results);
            Assert.Equal(7, results.Count);
            Assert.All(results, r => Assert.Equal(SecurityValidationState.Passed, r.ValidationState));

            // Cleanup
            if (File.Exists(configPath)) File.Delete(configPath);
        }

        [Fact]
        public async Task RunFullValidation_ShouldCancelGracefully_WhenTokenIsSignalled()
        {
            // Arrange
            var cts = new CancellationTokenSource();
            cts.Cancel(); // Pre-canceled!

            var service = new SecurityHardeningService(_loggerMock.Object, _serviceProvider, _eventDispatcherMock.Object);

            // Act & Assert
            await Assert.ThrowsAsync<OperationCanceledException>(() => service.RunFullValidationAsync(cts.Token));
        }

        #endregion
    }
}
