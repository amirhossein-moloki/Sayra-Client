using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Sayra.Client.LocalAdmin.Authentication;
using Sayra.Client.LocalAdmin.Models;
using Sayra.Client.LocalAdmin.Security;
using Sayra.Client.LocalAdmin.Services;
using Sayra.Client.LocalAdmin.Storage;

namespace Sayra.Client.Tests
{
    public class LocalAdminTests : IDisposable
    {
        private readonly string _testDataDir;

        public LocalAdminTests()
        {
            // Unique folder for each test execution to prevent interference
            _testDataDir = Path.Combine(Path.GetTempPath(), "SayraTestLocalAdmin_" + Guid.NewGuid().ToString());
            if (Directory.Exists(_testDataDir))
            {
                Directory.Delete(_testDataDir, true);
            }
            Directory.CreateDirectory(_testDataDir);
        }

        public void Dispose()
        {
            if (Directory.Exists(_testDataDir))
            {
                try
                {
                    Directory.Delete(_testDataDir, true);
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
        }

        [Fact]
        public async Task Test1_AdminCreation_WithValidCredentials_ShouldSucceed()
        {
            // Arrange
            var repo = new LocalAdminRepository(_testDataDir, null);
            var hasher = new PasswordHasher();
            var sessionManager = new AdminSessionManager();
            var service = new LocalAdminService(repo, hasher, sessionManager, null);

            // Act
            bool created = await service.CreateAdmin("staff_admin", "SecurePass123!");

            // Assert
            Assert.True(created);
            var credentials = (await repo.LoadCredentialsAsync()).ToList();
            Assert.Single(credentials);
            Assert.Equal("staff_admin", credentials[0].Username);
            Assert.False(string.IsNullOrEmpty(credentials[0].PasswordHash));
            Assert.False(string.IsNullOrEmpty(credentials[0].Salt));
        }

        [Fact]
        public async Task Test2_SuccessfulAuthentication_ShouldReturnSessionToken()
        {
            // Arrange
            var repo = new LocalAdminRepository(_testDataDir, null);
            var hasher = new PasswordHasher();
            var sessionManager = new AdminSessionManager();
            var service = new LocalAdminService(repo, hasher, sessionManager, null);

            await service.CreateAdmin("admin", "AdminPassword123!");

            // Act
            var authResult = await service.Authenticate("admin", "AdminPassword123!");

            // Assert
            Assert.True(authResult.Success);
            Assert.Null(authResult.ErrorReason);
            Assert.False(string.IsNullOrEmpty(authResult.SessionToken));
            Assert.True(sessionManager.ValidateSession(authResult.SessionToken!));
        }

        [Fact]
        public async Task Test3_WrongPasswordRejection_ShouldFail()
        {
            // Arrange
            var repo = new LocalAdminRepository(_testDataDir, null);
            var hasher = new PasswordHasher();
            var sessionManager = new AdminSessionManager();
            var service = new LocalAdminService(repo, hasher, sessionManager, null);

            await service.CreateAdmin("manager", "Manager123!");

            // Act
            var authResult = await service.Authenticate("manager", "WrongPassword!");

            // Assert
            Assert.False(authResult.Success);
            Assert.Equal("Invalid credentials.", authResult.ErrorReason);
            Assert.Null(authResult.SessionToken);
        }

        [Fact]
        public async Task Test4_AccountLockout_AfterFiveFailedAttempts_ShouldLockAndEventuallyUnlock()
        {
            // Arrange
            var repo = new LocalAdminRepository(_testDataDir, null);
            var hasher = new PasswordHasher();
            var sessionManager = new AdminSessionManager();
            var service = new LocalAdminService(repo, hasher, sessionManager, null);

            await service.CreateAdmin("locked_user", "ValidPass1!");

            // Act & Assert failed attempts
            for (int i = 0; i < 4; i++)
            {
                var attempt = await service.Authenticate("locked_user", "WrongPass!");
                Assert.False(attempt.Success);
                Assert.Equal("Invalid credentials.", attempt.ErrorReason);
            }

            // Check that the account is not locked yet (failed attempts = 4)
            var credentials = (await repo.LoadCredentialsAsync()).ToList();
            Assert.Equal(4, credentials[0].FailedAttempts);
            Assert.False(credentials[0].IsLocked);

            // 5th failed attempt -> lock account
            var fifthAttempt = await service.Authenticate("locked_user", "WrongPass!");
            Assert.False(fifthAttempt.Success);

            credentials = (await repo.LoadCredentialsAsync()).ToList();
            Assert.True(credentials[0].IsLocked);
            Assert.NotNull(credentials[0].LockedUntil);
            Assert.True(credentials[0].LockedUntil > DateTime.UtcNow);

            // Attempt with correct password should still fail because of lockout
            var attemptWithCorrectPass = await service.Authenticate("locked_user", "ValidPass1!");
            Assert.False(attemptWithCorrectPass.Success);
            Assert.Contains("locked", attemptWithCorrectPass.ErrorReason?.ToLowerInvariant() ?? "");

            // Simulate lockout time passing (set LockedUntil in the past)
            credentials[0].LockedUntil = DateTime.UtcNow.AddMinutes(-1);
            await repo.SaveCredentialsAsync(credentials);

            // Logging in with correct password should unlock and succeed
            var resolvedAttempt = await service.Authenticate("locked_user", "ValidPass1!");
            Assert.True(resolvedAttempt.Success);
            Assert.False(string.IsNullOrEmpty(resolvedAttempt.SessionToken));

            // Verify db was updated and lockout state cleared
            credentials = (await repo.LoadCredentialsAsync()).ToList();
            Assert.False(credentials[0].IsLocked);
            Assert.Null(credentials[0].LockedUntil);
            Assert.Equal(0, credentials[0].FailedAttempts);
        }

        [Fact]
        public async Task Test5_PasswordChange_ShouldWorkWithNewPasswordAndRejectOld()
        {
            // Arrange
            var repo = new LocalAdminRepository(_testDataDir, null);
            var hasher = new PasswordHasher();
            var sessionManager = new AdminSessionManager();
            var service = new LocalAdminService(repo, hasher, sessionManager, null);

            await service.CreateAdmin("operator", "OldPassword1!");

            // Act
            bool changed = await service.ChangePassword("operator", "OldPassword1!", "NewPassword2!");

            // Assert
            Assert.True(changed);

            // Old password must fail
            var oldAuth = await service.Authenticate("operator", "OldPassword1!");
            Assert.False(oldAuth.Success);

            // New password must succeed
            var newAuth = await service.Authenticate("operator", "NewPassword2!");
            Assert.True(newAuth.Success);
            Assert.False(string.IsNullOrEmpty(newAuth.SessionToken));
        }

        [Fact]
        public async Task Test6_SessionExpiration_ShouldInvalidateToken()
        {
            // Arrange
            var sessionManager = new AdminSessionManager();

            // Act
            string token = sessionManager.CreateSession("admin_user", TimeSpan.FromMilliseconds(100));
            Assert.True(sessionManager.ValidateSession(token));

            // Wait for expiration
            await Task.Delay(150);

            // Assert
            Assert.False(sessionManager.ValidateSession(token));
        }

        [Fact]
        public async Task Test7_CorruptedCredentialFileRecovery_ShouldRestoreFromBackup()
        {
            // Arrange
            var repo = new LocalAdminRepository(_testDataDir, null);

            var credentials = new List<LocalAdminCredential>
            {
                new LocalAdminCredential
                {
                    Id = Guid.NewGuid().ToString(),
                    Username = "backup_admin",
                    PasswordHash = "fakehash",
                    Salt = "fakesalt",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                }
            };

            // First write
            await repo.SaveCredentialsAsync(credentials);
            // Second write (creates the backup)
            await repo.SaveCredentialsAsync(credentials);

            string mainFilePath = Path.Combine(_testDataDir, "LocalAdmin", "admin_credentials.json");
            string backupFilePath = Path.Combine(_testDataDir, "LocalAdmin", "admin_credentials.json.bak");

            Assert.True(File.Exists(mainFilePath), "Main credentials file should exist.");
            Assert.True(File.Exists(backupFilePath), "Backup credentials file should exist.");

            // Corrupt main file
            await File.WriteAllTextAsync(mainFilePath, "{ corrupted invalid json: ");

            // Act
            var recoveryRepo = new LocalAdminRepository(_testDataDir, null);
            var loaded = (await recoveryRepo.LoadCredentialsAsync()).ToList();

            // Assert
            Assert.Single(loaded);
            Assert.Equal("backup_admin", loaded[0].Username);

            // Verify restore happened
            string restoredContent = await File.ReadAllTextAsync(mainFilePath);
            Assert.Contains("backup_admin", restoredContent);
        }

        [Fact]
        public async Task Test8_ConfigurationPersistence_ShouldSaveAndReloadSuccessfully()
        {
            // Arrange
            var configRepo1 = new ClientConfigurationRepository(_testDataDir, null);
            var configService1 = new ClientConfigurationService(configRepo1);

            var originalConfig = new ClientConfiguration
            {
                ServerDiscovery = new ServerDiscoverySettings
                {
                    ServerIp = "192.168.1.100",
                    UdpPort = 9999,
                    AutoDiscovery = false
                },
                GameLibrary = new GameLibrarySettings
                {
                    LibraryPath = "D:\\Games",
                    AutoUpdate = false
                },
                ScannerPaths = new List<string> { "D:\\Games\\Steam", "D:\\Games\\Epic" },
                LocalPreferences = new LocalPreferencesSettings
                {
                    Theme = "Light",
                    Language = "en-US",
                    IsKioskMode = false
                }
            };

            // Act
            await configService1.SaveConfigurationAsync(originalConfig);

            // Simulated restart -> reload from another repository instance pointing to the same dir
            var configRepo2 = new ClientConfigurationRepository(_testDataDir, null);
            var configService2 = new ClientConfigurationService(configRepo2);
            var loadedConfig = await configService2.GetConfigurationAsync();

            // Assert
            Assert.NotNull(loadedConfig);
            Assert.Equal("192.168.1.100", loadedConfig.ServerDiscovery.ServerIp);
            Assert.Equal(9999, loadedConfig.ServerDiscovery.UdpPort);
            Assert.False(loadedConfig.ServerDiscovery.AutoDiscovery);

            Assert.Equal("D:\\Games", loadedConfig.GameLibrary.LibraryPath);
            Assert.False(loadedConfig.GameLibrary.AutoUpdate);

            Assert.Equal(2, loadedConfig.ScannerPaths.Count);
            Assert.Contains("D:\\Games\\Steam", loadedConfig.ScannerPaths);
            Assert.Contains("D:\\Games\\Epic", loadedConfig.ScannerPaths);

            Assert.Equal("Light", loadedConfig.LocalPreferences.Theme);
            Assert.Equal("en-US", loadedConfig.LocalPreferences.Language);
            Assert.False(loadedConfig.LocalPreferences.IsKioskMode);
        }
    }
}
