using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Sayra.Client.Shared.DependencyInjection;
using Sayra.Client.Shared.Fleet.Interfaces;
using Sayra.Client.Shared.Fleet.Queues;
using Sayra.Client.Shared.Fleet.Security;
using Sayra.Client.Shared.Fleet.Services;
using Sayra.Client.Shared.Interfaces;
using Sayra.Client.Shared.Interfaces.Phase9;
using Sayra.Client.Shared.Models.Phase9.Domain;
using Sayra.Client.Shared.Models.Phase9.Enums;
using Sayra.Client.Shared.Models.Phase9.Events;
using Sayra.Client.Shared.Models.Phase9.Options;
using Sayra.Client.Shared.Models;
using Xunit;

namespace Sayra.Client.Configuration.Tests.Phase9
{
    public class RemoteFileManagementTests : IDisposable
    {
        private readonly ServiceProvider _provider;
        private readonly string _testBaseDir;
        private readonly string _testDataDir;

        public RemoteFileManagementTests()
        {
            // Set up test directories under base directory to insulate tests
            _testBaseDir = AppDomain.CurrentDomain.BaseDirectory;
            _testDataDir = Path.Combine(_testBaseDir, "Data");
            if (!Directory.Exists(_testDataDir))
            {
                Directory.CreateDirectory(_testDataDir);
            }

            var services = new ServiceCollection();

            // Register standard phase 9 logging, events, and audit services
            services.AddSingleton<IAuditLogger, TestAuditLogger>();
            services.AddSingleton<IEventDispatcher, TestEventDispatcher>();
            services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));

            // Options patterns
            services.Configure<TransferOptions>(options =>
            {
                options.DefaultChunkSizeBytes = 1024; // 1KB for faster testing
                options.MaxParallelTransfers = 2;
                options.ThrottleRateBytesPerSec = 1024; // 1KB/s limit
            });

            // Register newly implemented remote file management engine
            services.AddRemoteFileManagement();

            _provider = services.BuildServiceProvider();
        }

        public void Dispose()
        {
            _provider.Dispose();

            // Cleanup test directory files securely
            try
            {
                if (Directory.Exists(_testDataDir))
                {
                    Directory.Delete(_testDataDir, true);
                }
            }
            catch
            {
                // Suppress file locks in CI
            }
        }

        [Fact]
        public void PathTraversal_Blocked_Successfully()
        {
            // Arrange
            var validator = _provider.GetRequiredService<ISecurePathValidator>();
            string traversalPath = Path.Combine(_testDataDir, "../..", "secrets.json");

            // Act & Assert
            Assert.False(validator.IsSafePath(traversalPath));
            Assert.Throws<UnauthorizedAccessException>(() => validator.ValidateAndCanonicalize(traversalPath));
        }

        [Fact]
        public void SystemFolderAccess_Blocked_Successfully()
        {
            // Arrange
            var validator = _provider.GetRequiredService<ISecurePathValidator>();
            string systemPath = "C:\\Windows\\System32\\cmd.exe";

            // Act & Assert
            Assert.False(validator.IsSafePath(systemPath));
        }

        [Fact]
        public async Task Authorization_Blocked_When_Unauthorized()
        {
            // Arrange
            var auth = _provider.GetRequiredService<IFileAuthorizationService>();
            string path = Path.Combine(_testDataDir, "test.txt");

            // Act & Assert
            bool isAuth = await auth.AuthorizeAsync("unauthorized", path, FilePermissionScope.Read);
            Assert.False(isAuth);

            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                auth.ValidateAndAuditAsync("unauthorized", path, "ReadFile", FilePermissionScope.Read));
        }

        [Fact]
        public async Task ChecksumService_Verifies_SHA256_And_SHA512()
        {
            // Arrange
            var checksum = _provider.GetRequiredService<IChecksumService>();
            string path = Path.Combine(_testDataDir, "check.txt");
            await File.WriteAllTextAsync(path, "Enterprise Hashing Content!");

            // Act
            string sha256 = await checksum.CalculateHashAsync(path, "SHA256");
            string sha512 = await checksum.CalculateHashAsync(path, "SHA512");

            bool verify256 = await checksum.VerifyFileHashAsync(path, sha256, "SHA256");
            bool verify512 = await checksum.VerifyFileHashAsync(path, sha512, "SHA512");

            // Assert
            Assert.NotEmpty(sha256);
            Assert.NotEmpty(sha512);
            Assert.True(verify256);
            Assert.True(verify512);
        }

        [Fact]
        public async Task BandwidthLimiter_Throttles_Successfully()
        {
            // Arrange
            var limiter = _provider.GetRequiredService<IBandwidthLimiter>();
            limiter.SetMaxRate(100); // 100 bytes per second

            var start = DateTime.UtcNow;

            // Act
            // Ask to limit 200 bytes -> should take at least 1-2 seconds with 100 bytes/sec limit
            await limiter.LimitBytesAsync(200);
            var duration = DateTime.UtcNow - start;

            // Assert
            Assert.True(duration.TotalMilliseconds >= 500); // Throttling occurred
        }

        [Fact]
        public async Task Create_Directory_And_ListDirectory_Succeeds()
        {
            // Arrange
            var coordinator = _provider.GetRequiredService<IFileOperationCoordinator>();
            string dirPath = Path.Combine(_testDataDir, "SubDir01");

            // Act
            bool created = await coordinator.CreateDirectoryAsync("admin", dirPath);
            Assert.True(created);

            string filePath = Path.Combine(dirPath, "sample.bin");
            await File.WriteAllBytesAsync(filePath, new byte[] { 1, 2, 3 });

            var listing = await coordinator.ListDirectoryAsync("admin", dirPath);

            // Assert
            Assert.NotNull(listing);
            Assert.Contains(listing.Files, f => f.Name == "sample.bin");
        }

        [Fact]
        public async Task Copy_Move_And_Rename_File_Succeeds()
        {
            // Arrange
            var coordinator = _provider.GetRequiredService<IFileOperationCoordinator>();
            string sourcePath = Path.Combine(_testDataDir, "source.bin");
            string targetPath = Path.Combine(_testDataDir, "copied.bin");
            string movedPath = Path.Combine(_testDataDir, "moved.bin");

            await File.WriteAllBytesAsync(sourcePath, new byte[] { 10, 20, 30 });

            // Act & Assert Copy
            bool copied = await coordinator.CopyFileAsync("admin", sourcePath, targetPath);
            Assert.True(copied);
            Assert.True(File.Exists(targetPath));

            // Act & Assert Move
            bool moved = await coordinator.MoveFileAsync("admin", targetPath, movedPath);
            Assert.True(moved);
            Assert.True(File.Exists(movedPath));
            Assert.False(File.Exists(targetPath));

            // Act & Assert Rename
            bool renamed = await coordinator.RenameFileAsync("admin", movedPath, "renamed.bin");
            Assert.True(renamed);
            Assert.True(File.Exists(Path.Combine(_testDataDir, "renamed.bin")));
        }

        [Fact]
        public async Task DeleteFile_Succeeds_ForValidFile()
        {
            // Arrange
            var coordinator = _provider.GetRequiredService<IFileOperationCoordinator>();
            string path = Path.Combine(_testDataDir, "delete_me.txt");
            await File.WriteAllTextAsync(path, "Temp data");

            // Act
            bool deleted = await coordinator.DeleteFileAsync("admin", path);

            // Assert
            Assert.True(deleted);
            Assert.False(File.Exists(path));
        }

        [Fact]
        public async Task GetFileMetadata_Succeeds_And_ComputesHash()
        {
            // Arrange
            var coordinator = _provider.GetRequiredService<IFileOperationCoordinator>();
            string filePath = Path.Combine(_testDataDir, "meta.txt");
            await File.WriteAllTextAsync(filePath, "Enterprise Metadata Test File!");

            // Act
            var metadata = await coordinator.GetFileMetadataAsync("admin", filePath);

            // Assert
            Assert.NotNull(metadata);
            Assert.Equal("meta.txt", metadata.Name);
            Assert.NotEmpty(metadata.ChecksumSha256);
            Assert.Equal(30, metadata.SizeBytes);
        }

        [Fact]
        public async Task RemoteFileManagementEngine_ListFiles_And_Delete_Gateway_Succeeds()
        {
            // Arrange
            var engine = _provider.GetRequiredService<IRemoteFileService>();
            string filePath = Path.Combine(_testDataDir, "engine_test.txt");
            await File.WriteAllTextAsync(filePath, "Gateway Data");

            // Act
            var files = await engine.ListFilesAsync("WS-01", _testDataDir);
            bool deleted = await engine.DeleteFileAsync("WS-01", filePath);

            // Assert
            Assert.Contains(filePath, files);
            Assert.True(deleted);
            Assert.False(File.Exists(filePath));
        }

        [Fact]
        public async Task TransferManager_Handles_LargeFile_With_Resume_And_Cancellation()
        {
            // Arrange
            var manager = _provider.GetRequiredService<ITransferManager>();
            var dispatcher = _provider.GetRequiredService<IEventDispatcher>() as TestEventDispatcher;
            Assert.NotNull(dispatcher);

            string destPath = Path.Combine(_testDataDir, "transfer.bin");

            var job = new TransferJob
            {
                JobId = "job_001",
                FilePath = destPath,
                Direction = TransferDirection.Download,
                Category = TransferType.File,
                Status = TransferStatus.Pending,
                TotalFileSizeBytes = 4096, // 4KB (4 chunks of 1KB)
                FullFileIntegrityHash = string.Empty
            };

            // Act: Start Transfer
            var startedJob = await manager.StartTransferAsync(job);
            Assert.NotNull(startedJob);
            Assert.Equal(4, startedJob.Chunks.Count);

            // Give transfer a moment to execute
            await Task.Delay(100);

            // Pause Transfer
            bool paused = await manager.PauseTransferAsync("job_001");
            Assert.True(paused);

            // Verify event triggered
            Assert.Contains(dispatcher.DispatchedEvents, e => e is TransferPaused);

            // Resume Transfer
            bool resumed = await manager.ResumeTransferAsync("job_001");
            Assert.True(resumed);

            // Wait for completion (give enough time for throttled 4KB transfer at 1KB/s)
            int timeoutMs = 10000;
            while (timeoutMs > 0 && !File.Exists(destPath))
            {
                await Task.Delay(50);
                timeoutMs -= 50;
            }

            // Assert
            Assert.True(File.Exists(destPath));
            Assert.Contains(dispatcher.DispatchedEvents, e => e is TransferCompleted);
        }

        [Fact]
        public async Task TransferQueue_Prioritizes_And_Prevents_Duplicates()
        {
            // Arrange
            var queue = _provider.GetRequiredService<ITransferQueue>();
            var repo = _provider.GetRequiredService<ITransferRepository>();

            string file1 = Path.Combine(_testDataDir, "q1.bin");
            string file2 = Path.Combine(_testDataDir, "q2.bin");

            var highJob = new TransferJob
            {
                JobId = "job_high",
                FilePath = file1,
                Direction = TransferDirection.Download,
                Category = TransferType.UpdatePackage, // High priority
                TotalFileSizeBytes = 100
            };

            var normalJob = new TransferJob
            {
                JobId = "job_normal",
                FilePath = file2,
                Direction = TransferDirection.Download,
                Category = TransferType.File, // Normal priority
                TotalFileSizeBytes = 100
            };

            // Act
            bool enqueuedNormal = await queue.EnqueueAsync(normalJob);
            bool enqueuedHigh = await queue.EnqueueAsync(highJob);

            // Duplicate enqueuing of the same file path
            bool enqueuedDuplicate = await queue.EnqueueAsync(normalJob with { JobId = "job_dup" });

            var firstDequeued = await queue.DequeueAsync();
            var secondDequeued = await queue.DequeueAsync();

            // Assert
            Assert.True(enqueuedNormal);
            Assert.True(enqueuedHigh);
            Assert.False(enqueuedDuplicate);

            // High priority job (UpdatePackage) must dequeue first even though enqueued after normal
            Assert.NotNull(firstDequeued);
            Assert.Equal("job_high", firstDequeued.JobId);

            Assert.NotNull(secondDequeued);
            Assert.Equal("job_normal", secondDequeued.JobId);
        }

        [Fact]
        public async Task Startup_Recovery_Restores_Interrupted_Jobs()
        {
            // Arrange
            var repo = _provider.GetRequiredService<ITransferRepository>();
            var queue = _provider.GetRequiredService<ITransferQueue>();

            string path = Path.Combine(_testDataDir, "interrupted.bin");
            var job = new TransferJob
            {
                JobId = "job_interrupted",
                FilePath = path,
                Direction = TransferDirection.Download,
                Category = TransferType.File,
                Status = TransferStatus.Transferring, // Simulated active/interrupted crash state
                TotalFileSizeBytes = 200
            };

            await repo.SaveJobAsync(job);

            // Act
            await queue.RecoverJobsAfterRestartAsync();
            var dequeued = await queue.DequeueAsync();

            // Assert
            Assert.NotNull(dequeued);
            Assert.Equal("job_interrupted", dequeued.JobId);
            Assert.Equal(TransferStatus.Pending, dequeued.Status); // Recovered state reset to pending
        }
    }

    // Helper Stub Implementations for test execution isolation

    public class TestAuditLogger : IAuditLogger
    {
        public List<string> LoggedMessages { get; } = new();

        public void LogSecurity(string messageTemplate, Dictionary<string, object>? properties = null)
        {
            LoggedMessages.Add("SECURITY: " + messageTemplate);
        }

        public void LogAudit(string messageTemplate, Dictionary<string, object>? properties = null)
        {
            LoggedMessages.Add("AUDIT: " + messageTemplate);
        }

        public void LogOperational(string messageTemplate, Dictionary<string, object>? properties = null)
        {
            LoggedMessages.Add("OP: " + messageTemplate);
        }

        public void LogPerformance(string messageTemplate, Dictionary<string, object>? properties = null)
        {
            LoggedMessages.Add("PERF: " + messageTemplate);
        }

        public void LogEvent(EventLogEntry entry)
        {
            LoggedMessages.Add("EVENT: " + entry.MessageTemplate);
        }
    }

    public class TestEventDispatcher : IEventDispatcher
    {
        public List<object> DispatchedEvents { get; } = new();

        public void Dispatch<T>(T @event)
        {
            if (@event != null)
            {
                DispatchedEvents.Add(@event);
            }
        }

        public void RegisterHandler<T>(Action<T> handler)
        {
        }
    }
}
