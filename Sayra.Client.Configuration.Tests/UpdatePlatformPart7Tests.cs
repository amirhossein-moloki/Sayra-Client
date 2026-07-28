using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;
using Xunit;
using Sayra.Client.Shared.Interfaces.Security;
using Sayra.Client.Shared.UpdatePlatform.Application.Interfaces;
using Sayra.Client.Shared.UpdatePlatform.Application.Services;
using Sayra.Client.Shared.UpdatePlatform.Domain.Exceptions;
using Sayra.Client.Shared.UpdatePlatform.Domain.Models;
using Sayra.Client.Shared.UpdatePlatform.Domain.Options;

namespace Sayra.Client.Configuration.Tests
{
    /// <summary>
    /// Comprehensive enterprise-grade test suite for Phase 6 Part 7: Update Storage, Cache & History.
    /// </summary>
    public class UpdatePlatformPart7Tests : IDisposable
    {
        private readonly string _testTempDir;
        private readonly string _dbPath;
        private readonly string _cacheDir;
        private readonly IOptions<StorageOptions> _options;

        public UpdatePlatformPart7Tests()
        {
            _testTempDir = Path.Combine(AppContext.BaseDirectory, $"Part7Test_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_testTempDir);

            _dbPath = Path.Combine(_testTempDir, "test_update_platform.db");
            _cacheDir = Path.Combine(_testTempDir, "test_cache");
            Directory.CreateDirectory(_cacheDir);

            var storageOptions = new StorageOptions
            {
                DatabasePath = _dbPath,
                CacheDirectory = _cacheDir,
                MaxCacheSizeMegabytes = 2, // Small 2MB ceiling for LRU eviction tests
                ReservedRollbackSpaceMegabytes = 1,
                CacheExpirationDays = 1
            };

            _options = Options.Create(storageOptions);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_testTempDir))
                {
                    Directory.Delete(_testTempDir, recursive: true);
                }
            }
            catch
            {
                // Ignore transient cleanup errors
            }
        }

        #region SQLCipher Initialization & Database Migration Tests

        [Fact]
        public async Task DatabaseMigration_ShouldCreateCorrectSchemaAndTables()
        {
            // Arrange
            var migrationService = new DatabaseMigrationService(_options);

            // Act
            await migrationService.MigrateAsync();
            int currentVersion = await migrationService.GetCurrentVersionAsync();

            // Assert
            Assert.Equal(1, currentVersion);

            // Verify tables physically exist in SQLCipher DB
            var connBuilder = new SqliteConnectionStringBuilder
            {
                DataSource = _dbPath,
                Password = Sayra.Client.Shared.Security.Crypto.DatabaseKeyManager.GetOrInitializeKey(null)
            };

            using var connection = new SqliteConnection(connBuilder.ConnectionString);
            await connection.OpenAsync();

            var tables = new List<string>();
            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table';";
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    tables.Add(reader.GetString(0));
                }
            }

            Assert.Contains("DbVersion", tables);
            Assert.Contains("UpdateHistory", tables);
            Assert.Contains("RollbackLogs", tables);
            Assert.Contains("CacheEntries", tables);
        }

        [Fact]
        public async Task SQLCipher_EncryptionAtRest_VerifyTamperingAndLockdown()
        {
            // Arrange
            var migrationService = new DatabaseMigrationService(_options);
            await migrationService.MigrateAsync();

            // Act & Assert: Attempting to open SQLCipher database with an empty or incorrect password must fail-closed.
            var badConnBuilder = new SqliteConnectionStringBuilder
            {
                DataSource = _dbPath,
                Password = "INCORRECT_DECRYPTION_PASSWORD_KEY"
            };

            using var connection = new SqliteConnection(badConnBuilder.ConnectionString);
            var ex = await Assert.ThrowsAnyAsync<Exception>(async () => await connection.OpenAsync());
            Assert.NotNull(ex);
        }

        #endregion

        #region Repository CRUD & History Persistence Tests

        [Fact]
        public async Task UpdateHistoryRepository_CRUD_ShouldPersistAndQueryCorrectly()
        {
            // Arrange
            var migrationService = new DatabaseMigrationService(_options);
            await migrationService.MigrateAsync();

            var repository = new UpdateHistoryRepository(_options);

            var record = new UpdateHistoryRecord
            {
                Id = Guid.NewGuid(),
                PackageId = Guid.NewGuid(),
                Version = "2.5.0-rc1",
                PreviousVersion = "2.4.0",
                InstallationTime = DateTime.UtcNow.AddMinutes(-5),
                CompletionTime = DateTime.UtcNow,
                Status = "COMPLETED",
                Duration = TimeSpan.FromSeconds(300),
                ErrorCode = "0",
                Result = "SUCCESS",
                DeviceIdentifier = "WORKSTATION-X99",
                TelemetryUploaded = false
            };

            // Act - Insert
            await repository.InsertAsync(record);

            // Act - Query By ID
            var queried = await repository.GetByIdAsync(record.Id);
            Assert.NotNull(queried);
            Assert.Equal(record.Version, queried.Version);
            Assert.Equal(record.Status, queried.Status);
            Assert.Equal(record.DeviceIdentifier, queried.DeviceIdentifier);
            Assert.Equal(300, queried.Duration.TotalSeconds);

            // Act - Update
            queried.Status = "ROLLED_BACK";
            queried.ErrorCode = "ERR_CRASH";
            await repository.UpdateAsync(queried);

            // Act - Query Latest & All
            var latest = await repository.GetLatestAsync();
            Assert.NotNull(latest);
            Assert.Equal("ROLLED_BACK", latest.Status);
            Assert.Equal("ERR_CRASH", latest.ErrorCode);

            var all = await repository.GetAllAsync();
            Assert.Single(all);

            // Act - Cleanup
            await repository.CleanupAsync(DateTime.UtcNow.AddHours(1));
            var emptyAll = await repository.GetAllAsync();
            Assert.Empty(emptyAll);
        }

        [Fact]
        public async Task RollbackHistoryRepository_CRUD_ShouldPersistAndQueryCorrectly()
        {
            // Arrange
            var migrationService = new DatabaseMigrationService(_options);
            await migrationService.MigrateAsync();

            var repository = new RollbackHistoryRepository(_options);

            var record = new RollbackHistoryRecord
            {
                Id = Guid.NewGuid(),
                Reason = "Service initialization timeout after version 2.5.0",
                TriggerSource = "WATCHDOG_MONITOR",
                PreviousVersion = "2.5.0",
                RestoredVersion = "2.4.0",
                Duration = TimeSpan.FromSeconds(45),
                Result = "SUCCESS",
                FailureDetails = "System.TimeoutException: SayraClient failed to boot in 30s.",
                Timestamp = DateTime.UtcNow.AddMinutes(-2)
            };

            // Act - Insert
            await repository.InsertAsync(record);

            // Act - Query By ID
            var queried = await repository.GetByIdAsync(record.Id);
            Assert.NotNull(queried);
            Assert.Equal(record.Reason, queried.Reason);
            Assert.Equal(record.TriggerSource, queried.TriggerSource);
            Assert.Equal(record.FailureDetails, queried.FailureDetails);

            // Act - Query All
            var all = await repository.GetAllAsync();
            Assert.Single(all);

            // Act - Cleanup
            await repository.CleanupAsync(DateTime.UtcNow.AddHours(1));
            var emptyAll = await repository.GetAllAsync();
            Assert.Empty(emptyAll);
        }

        #endregion

        #region Cache Manager, Eviction & Validation Tests

        [Fact]
        public async Task CacheManager_ShouldAddGetAndTrackCacheEntries()
        {
            // Arrange
            var migrationService = new DatabaseMigrationService(_options);
            await migrationService.MigrateAsync();

            var cacheManager = new CacheManager(_options);

            string fileKey = "test-pkg-v1";
            string filePath = Path.Combine(_cacheDir, "pkg_v1.spk");
            await File.WriteAllTextAsync(filePath, "DUMMY PACKAGE BYTES FOR SPK DATA FILE");

            string sha256 = "6F85AF7F3B5FBE990D069FECE1C18D532EDEA103C61AAED867DA960CA2537D62"; // Placeholder or actual

            // Act
            var entry = await cacheManager.AddOrUpdateAsync(
                key: fileKey,
                filePath: filePath,
                entryType: "Package",
                version: "1.0.0",
                sizeBytes: 100,
                sha256Hash: sha256
            );

            // Assert
            Assert.Equal(fileKey, entry.Key);
            Assert.True(File.Exists(filePath));

            var queried = await cacheManager.GetAsync(fileKey);
            Assert.NotNull(queried);
            Assert.Equal(filePath, queried.FilePath);
            Assert.Equal("Package", queried.EntryType);

            // Evict
            await cacheManager.EvictAsync(fileKey);
            Assert.False(File.Exists(filePath));
            Assert.Null(await cacheManager.GetAsync(fileKey));
        }

        [Fact]
        public async Task CacheManager_LRU_Eviction_ShouldEvictOldestUnlockedFilesWhenCeilingExceeded()
        {
            // Arrange
            var migrationService = new DatabaseMigrationService(_options);
            await migrationService.MigrateAsync();

            var cacheManager = new CacheManager(_options);

            // We configured MaxCacheSizeMegabytes = 2MB (2,097,152 bytes)
            // Let's write 3 files, each 1.1MB (1,153,433 bytes) -> Total 3.3MB (exceeds 2MB limit)
            byte[] fileBytes = new byte[1100 * 1024]; // 1.1MB

            string path1 = Path.Combine(_cacheDir, "file1.spk");
            string path2 = Path.Combine(_cacheDir, "file2.spk");
            string path3 = Path.Combine(_cacheDir, "file3.spk");

            await File.WriteAllBytesAsync(path1, fileBytes);
            await File.WriteAllBytesAsync(path2, fileBytes);

            // Add first entry (LastAccessedAt will be set to UtcNow)
            await cacheManager.AddOrUpdateAsync("key1", path1, "Package", "1.0.0", fileBytes.Length, "HASH1");

            // Wait slightly to ensure different LastAccessedAt
            await Task.Delay(150);

            // Add second entry (exceeds limit on insert of file 2? No, 1.1MB + 1.1MB = 2.2MB > 2.0MB, so file 1 should be evict-controlled)
            await cacheManager.AddOrUpdateAsync("key2", path2, "Package", "1.0.0", fileBytes.Length, "HASH2");

            // Verify file1 is evited because total size is 2.2MB (exceeded 2MB)
            Assert.False(File.Exists(path1));
            Assert.True(File.Exists(path2));

            // Wait slightly
            await Task.Delay(150);

            // Now write and add file 3 (1.1MB)
            await File.WriteAllBytesAsync(path3, fileBytes);
            await cacheManager.AddOrUpdateAsync("key3", path3, "Package", "1.0.0", fileBytes.Length, "HASH3");

            // Since limit was exceeded again, file 2 (the oldest active) must have been evicted, and file 3 remains
            Assert.False(File.Exists(path2));
            Assert.True(File.Exists(path3));
        }

        [Fact]
        public async Task CacheManager_IntegrityCheck_ShouldThrowOnTamperedOrCorruptedFiles()
        {
            // Arrange
            var migrationService = new DatabaseMigrationService(_options);
            await migrationService.MigrateAsync();

            var cacheManager = new CacheManager(_options);

            string fileKey = "corrupt-test";
            string filePath = Path.Combine(_cacheDir, "corrupt.spk");
            byte[] cleanBytes = new byte[] { 0x01, 0x02, 0x03, 0x04 };
            await File.WriteAllBytesAsync(filePath, cleanBytes);

            string actualHash;
            using (var sha = System.Security.Cryptography.SHA256.Create())
            {
                actualHash = Convert.ToHexString(sha.ComputeHash(cleanBytes));
            }

            await cacheManager.AddOrUpdateAsync(fileKey, filePath, "Package", "1.0.0", cleanBytes.Length, actualHash);

            // Act - Integrity passes initially
            await cacheManager.ValidateIntegrityAsync(); // Should not throw

            // Act - Tamper the file
            await File.WriteAllBytesAsync(filePath, new byte[] { 0x99, 0x99, 0x99, 0x99 });

            // Assert - Detection and Exception
            var ex = await Assert.ThrowsAsync<CacheCorruptionException>(async () => await cacheManager.ValidateIntegrityAsync());
            Assert.Contains("Cache integrity validation failed", ex.Message);

            // Verify metadata is marked invalid in DB
            var entry = await cacheManager.GetAsync(fileKey);
            Assert.NotNull(entry);
            Assert.False(entry.IsValid);
        }

        [Fact]
        public async Task CacheManager_ClearInvalidAndFailed_ShouldPurgeCorrectly()
        {
            // Arrange
            var migrationService = new DatabaseMigrationService(_options);
            await migrationService.MigrateAsync();

            var cacheManager = new CacheManager(_options);

            // 1. Create a failed temporary download entry
            string tempPath = Path.Combine(_cacheDir, "failed.tmp");
            await File.WriteAllTextAsync(tempPath, "FAILED TEMP DATA");
            await cacheManager.AddOrUpdateAsync("temp-key", tempPath, "TemporaryDownload", "1.1.0", 100, "HASH_TMP");

            // 2. Create an unregistered physical file in cache folder
            string roguePath = Path.Combine(_cacheDir, "rogue_untracked.spk");
            await File.WriteAllTextAsync(roguePath, "UNTRACKED BYTES");

            // Assert they exist before
            Assert.True(File.Exists(tempPath));
            Assert.True(File.Exists(roguePath));

            // Act - Run cleanup sweep
            await cacheManager.ClearInvalidAndFailedAsync();

            // Assert - Files are cleared
            Assert.False(File.Exists(tempPath));
            Assert.False(File.Exists(roguePath));
        }

        #endregion

        #region Disk Space & Storage Quota Verification Tests

        [Fact]
        public async Task StorageQuotaManager_ShouldEvaluateSpaceAndReservedRollbackSuccessfully()
        {
            // Arrange
            var migrationService = new DatabaseMigrationService(_options);
            await migrationService.MigrateAsync();

            var quotaManager = new StorageQuotaManager(_options);

            // Act
            bool canStore1KB = await quotaManager.HasEnoughSpaceForPackageAsync(1024);
            var stats = await quotaManager.GetStatisticsAsync();

            // Assert
            Assert.True(canStore1KB);
            Assert.True(stats.TotalDiskSpaceBytes > 0);
            Assert.True(stats.AvailableFreeSpaceBytes > 0);
            Assert.Equal(2L * 1024 * 1024, stats.CacheLimitBytes); // 2MB
            Assert.Equal(1L * 1024 * 1024, stats.ReservedRollbackSpaceBytes); // 1MB
        }

        [Fact]
        public async Task StorageQuotaManager_ShouldThrowInsufficientDiskSpaceWhenExceedingCacheCeiling()
        {
            // Arrange
            var migrationService = new DatabaseMigrationService(_options);
            await migrationService.MigrateAsync();

            var quotaManager = new StorageQuotaManager(_options);

            // Package size is 2.5MB, but total cache ceiling is 2MB
            long hugePackageBytes = 2500 * 1024;

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InsufficientDiskSpaceException>(
                async () => await quotaManager.HasEnoughSpaceForPackageAsync(hugePackageBytes));
            Assert.Contains("exceeds the total configured cache ceiling", ex.Message);
        }

        #endregion

        #region Concurrency and Resiliency Tests

        [Fact]
        public async Task ConcurrentAccess_ShouldBeThreadSafeAndSuccessful()
        {
            // Arrange
            var migrationService = new DatabaseMigrationService(_options);
            await migrationService.MigrateAsync();

            var repository = new UpdateHistoryRepository(_options);

            int taskCount = 10;
            var tasks = new List<Task>();

            for (int i = 0; i < taskCount; i++)
            {
                int index = i;
                tasks.Add(Task.Run(async () =>
                {
                    var record = new UpdateHistoryRecord
                    {
                        Id = Guid.NewGuid(),
                        PackageId = Guid.NewGuid(),
                        Version = $"1.0.{index}",
                        PreviousVersion = "1.0.0",
                        InstallationTime = DateTime.UtcNow,
                        Status = "COMPLETED",
                        DeviceIdentifier = $"CONCURRENT-DEV-{index}"
                    };

                    await repository.InsertAsync(record);
                }));
            }

            // Act
            await Task.WhenAll(tasks);

            // Assert
            var all = await repository.GetAllAsync();
            Assert.Equal(taskCount, all.Count());
        }

        [Fact]
        public async Task RecoveryAfterDatabaseCorruption_ShouldRecreateDatabaseAndRecover()
        {
            // Arrange
            var migrationService = new DatabaseMigrationService(_options);
            var healthMonitor = new DatabaseHealthMonitor(_options);
            var recoveryService = new DatabaseRecoveryService(migrationService, _options);

            // 1. Initial Migrate and check health
            await migrationService.MigrateAsync();
            Assert.True(await healthMonitor.VerifyIntegrityAsync());
            Assert.True(await healthMonitor.ValidateSchemaAsync());

            // 2. Insert a history record
            var repository = new UpdateHistoryRepository(_options);
            var record = new UpdateHistoryRecord { Version = "2.1.0", DeviceIdentifier = "TEST_DEV" };
            await repository.InsertAsync(record);

            // Confirm it exists
            var initialList = await repository.GetAllAsync();
            Assert.Single(initialList);

            // 3. Act - Corrupt database file bytes physically to simulate unrecoverable corruption
            await File.WriteAllBytesAsync(_dbPath, new byte[] { 0xFF, 0xAA, 0xBB, 0xCC, 0x11, 0x22 });

            // Delete WAL and SHM files
            if (File.Exists(_dbPath + "-wal")) File.Delete(_dbPath + "-wal");
            if (File.Exists(_dbPath + "-shm")) File.Delete(_dbPath + "-shm");

            // Clear pool to release cache
            SqliteConnection.ClearAllPools();

            // 4. Verify Health Monitor detects the corruption
            bool isHealthy = await healthMonitor.VerifyIntegrityAsync();
            bool isSchemaValid = await healthMonitor.ValidateSchemaAsync();
            Assert.False(isHealthy && isSchemaValid);

            // 5. Verify Repository fails to query (throws exception)
            var corruptedRepo = new UpdateHistoryRepository(_options);
            var queryEx = await Assert.ThrowsAnyAsync<Exception>(async () => await corruptedRepo.GetAllAsync());
            Assert.NotNull(queryEx);

            // 6. Act - Invoke DatabaseRecoveryService to recover and recreate DB
            bool recoveryResult = await recoveryService.RecoverAndRecreateAsync(queryEx);
            Assert.True(recoveryResult);

            // 7. Assert - Verify Health Monitor reports database as healthy and schema as valid again
            Assert.True(await healthMonitor.VerifyIntegrityAsync());
            Assert.True(await healthMonitor.ValidateSchemaAsync());

            // 8. Assert - Querying the repository succeeds on the freshly recovered database
            var recoveredList = await repository.GetAllAsync();
            Assert.Empty(recoveredList);
        }

        #endregion
    }
}
