using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
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
    /// Comprehensive enterprise-grade test suite for Phase 6 Part 9: Telemetry, Monitoring & Administrative Integration.
    /// </summary>
    public class UpdatePlatformPart9Tests : IDisposable
    {
        private readonly string _testTempDir;
        private readonly string _dbPath;
        private readonly IOptions<StorageOptions> _storageOptions;
        private readonly IOptions<TelemetryOptions> _telemetryOptions;
        private readonly IOptions<MonitoringOptions> _monitoringOptions;
        private readonly IOptions<ReportingOptions> _reportingOptions;

        public UpdatePlatformPart9Tests()
        {
            _testTempDir = Path.Combine(AppContext.BaseDirectory, $"Part9Test_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_testTempDir);

            _dbPath = Path.Combine(_testTempDir, "test_update_platform.db");

            _storageOptions = Options.Create(new StorageOptions
            {
                DatabasePath = _dbPath,
                CacheDirectory = Path.Combine(_testTempDir, "cache")
            });

            _telemetryOptions = Options.Create(new TelemetryOptions
            {
                Enabled = true,
                QueueLimit = 5, // Keep it small for FIFO eviction testing
                ReportingIntervalSeconds = 1
            });

            _monitoringOptions = Options.Create(new MonitoringOptions
            {
                Enabled = true,
                MinStorageBytes = 10485760, // 10 MB
                CheckIntervalMinutes = 1
            });

            _reportingOptions = Options.Create(new ReportingOptions
            {
                Enabled = true,
                MaxRetryAttempts = 3,
                BaseDelaySeconds = 0
            });
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

        private async Task InitializeDatabaseAsync()
        {
            var migrationService = new DatabaseMigrationService(_storageOptions);
            await migrationService.MigrateAsync();
        }

        #region Event Creation & Validation Tests

        [Fact]
        public async Task RecordEvent_WithEmptyEventType_ShouldThrowTelemetryException()
        {
            // Arrange
            await InitializeDatabaseAsync();
            var queue = new TelemetryOfflineQueue(_storageOptions, _telemetryOptions);
            var adminClient = new AdminIntegrationClient();
            var reporter = new TelemetryReporter(queue, adminClient, NullLogger<TelemetryReporter>.Instance, _telemetryOptions, _reportingOptions);

            // Act & Assert
            await Assert.ThrowsAsync<TelemetryException>(async () =>
            {
                await reporter.RecordEventAsync(
                    eventType: "",
                    correlationId: "corr-123",
                    sourceVersion: "1.0.0",
                    targetVersion: "2.0.0",
                    success: true);
            });
        }

        [Fact]
        public async Task TelemetryEventModel_ShouldEnrichAndPopulateCorrectly()
        {
            // Arrange
            var ev = new UpdateTelemetryEvent
            {
                EventType = "TestEvent",
                CorrelationId = "correlation-abc",
                SourceVersion = "1.0.0",
                TargetVersion = "1.1.0",
                Success = true,
                ErrorCode = "0",
                ErrorMessage = "Success message",
                DeviceIdentifier = "WS-TEST-99",
                PayloadJson = "{\"custom\": 42}"
            };

            // Assert
            Assert.NotEqual(Guid.Empty, ev.EventId);
            Assert.Equal("TestEvent", ev.EventType);
            Assert.Equal("correlation-abc", ev.CorrelationId);
            Assert.Equal("1.0.0", ev.SourceVersion);
            Assert.Equal("1.1.0", ev.TargetVersion);
            Assert.True(ev.Success);
            Assert.Equal("0", ev.ErrorCode);
            Assert.Equal("Success message", ev.ErrorMessage);
            Assert.Equal("WS-TEST-99", ev.DeviceIdentifier);
            Assert.Contains("42", ev.PayloadJson);
            Assert.True((DateTime.UtcNow - ev.TimestampUtc).TotalMinutes < 1);
        }

        #endregion

        #region Offline Queue Buffer & Limits Tests

        [Fact]
        public async Task TelemetryOfflineQueue_EnqueueAndDequeueBatch_ShouldWorkAtomically()
        {
            // Arrange
            await InitializeDatabaseAsync();
            var queue = new TelemetryOfflineQueue(_storageOptions, _telemetryOptions);

            var ev1 = new UpdateTelemetryEvent { EventType = "UpdateStarted", CorrelationId = "corr-1", SourceVersion = "1.0" };
            var ev2 = new UpdateTelemetryEvent { EventType = "DownloadStarted", CorrelationId = "corr-2", SourceVersion = "1.0" };

            // Act
            await queue.EnqueueAsync(ev1);
            await queue.EnqueueAsync(ev2);

            int count = await queue.GetCountAsync();
            var batch = (await queue.DequeueBatchAsync(10)).ToList();

            // Assert
            Assert.Equal(2, count);
            Assert.Equal(2, batch.Count);
            Assert.Equal("UpdateStarted", batch[0].EventType);
            Assert.Equal("DownloadStarted", batch[1].EventType);

            // Delete batch
            await queue.DeleteBatchAsync(batch.Select(b => b.EventId));
            int remaining = await queue.GetCountAsync();
            Assert.Equal(0, remaining);
        }

        [Fact]
        public async Task TelemetryOfflineQueue_QueueLimit_ShouldEvictOldestFIFO()
        {
            // Arrange
            await InitializeDatabaseAsync();
            var queue = new TelemetryOfflineQueue(_storageOptions, _telemetryOptions); // QueueLimit is 5

            // Act - Enqueue 6 items (exceeds limit 5)
            for (int i = 1; i <= 6; i++)
            {
                var ev = new UpdateTelemetryEvent
                {
                    EventType = $"Event_{i}",
                    CorrelationId = $"corr-{i}",
                    TimestampUtc = DateTime.UtcNow.AddMinutes(i) // Ensure strict sequential timestamps
                };
                await queue.EnqueueAsync(ev);
                await Task.Delay(10); // Slight delay for deterministic insertion ordering
            }

            // Assert
            int count = await queue.GetCountAsync();
            Assert.Equal(5, count); // Enforces exact limit

            var pending = (await queue.DequeueBatchAsync(10)).ToList();
            // Oldest "Event_1" must be evicted, leaving Event_2, Event_3, Event_4, Event_5, Event_6
            Assert.DoesNotContain(pending, p => p.EventType == "Event_1");
            Assert.Contains(pending, p => p.EventType == "Event_2");
            Assert.Contains(pending, p => p.EventType == "Event_6");
        }

        [Fact]
        public async Task TelemetryOfflineQueue_ApplicationRestartRecovery_ShouldPreserveState()
        {
            // Arrange
            await InitializeDatabaseAsync();
            var queue1 = new TelemetryOfflineQueue(_storageOptions, _telemetryOptions);
            var ev = new UpdateTelemetryEvent { EventType = "StagedEvent", CorrelationId = "corr-restart" };

            await queue1.EnqueueAsync(ev);
            int countBefore = await queue1.GetCountAsync();

            // Act - Recreate queue reference (simulating process restart)
            var queue2 = new TelemetryOfflineQueue(_storageOptions, _telemetryOptions);
            int countAfter = await queue2.GetCountAsync();
            var restored = (await queue2.DequeueBatchAsync(1)).FirstOrDefault();

            // Assert
            Assert.Equal(1, countBefore);
            Assert.Equal(1, countAfter);
            Assert.NotNull(restored);
            Assert.Equal("StagedEvent", restored.EventType);
            Assert.Equal("corr-restart", restored.CorrelationId);
        }

        #endregion

        #region Retry & Exponential Backoff Tests

        [Fact]
        public async Task TelemetryReporter_WithNetworkFailure_ShouldBufferLocallyAndRetryOnFlush()
        {
            // Arrange
            await InitializeDatabaseAsync();
            var queue = new TelemetryOfflineQueue(_storageOptions, _telemetryOptions);
            var adminClient = new AdminIntegrationClient { SimulateNetworkFailure = true };
            using var reporter = new TelemetryReporter(queue, adminClient, NullLogger<TelemetryReporter>.Instance, _telemetryOptions, _reportingOptions);

            // Act
            await reporter.RecordEventAsync(
                eventType: "DownloadFailed",
                correlationId: "corr-retry-test",
                sourceVersion: "1.0",
                targetVersion: "1.1",
                success: false,
                errorCode: "ERR_TIMEOUT",
                errorMessage: "Simulated transit failure");

            // Give it a brief moment to process the fire-and-forget record call
            await Task.Delay(150);

            // Act - Force flush while offline
            await reporter.FlushAsync();

            // Assert - Since transmission failed, the event must remain in the offline queue
            int bufferedCount = await queue.GetCountAsync();
            Assert.Equal(1, bufferedCount);

            var pending = (await queue.DequeueBatchAsync(1)).First();
            Assert.Equal("DownloadFailed", pending.EventType);
            Assert.Equal("corr-retry-test", pending.CorrelationId);

            // Recover network and flush again
            adminClient.SimulateNetworkFailure = false;
            await reporter.FlushAsync();

            int finalCount = await queue.GetCountAsync();
            Assert.Equal(0, finalCount); // Successfully sent!
        }

        #endregion

        #region Health Monitoring & Diagnostic Reporting Tests

        [Fact]
        public async Task HealthMonitor_EvaluateHealth_ShouldReturnCorrectSubsystemStates()
        {
            // Arrange
            await InitializeDatabaseAsync();
            var mockStorage = new MockStorageQuotaManager(1024 * 1024 * 100, 1024 * 1024 * 5); // Low space (5MB < 10MB)
            var historyRepo = new UpdateHistoryRepository(_storageOptions);
            var mockDownloader = new MockDownloadManager();
            var mockInstaller = new MockInstallerEngine();
            var mockRollback = new MockRollbackEngine();

            var monitor = new HealthMonitor(
                mockStorage,
                historyRepo,
                mockDownloader,
                mockInstaller,
                mockRollback,
                _monitoringOptions,
                NullLogger<HealthMonitor>.Instance);

            // Act - Evaluate health under low storage condition
            var metric = await monitor.EvaluateHealthAsync();

            // Assert
            Assert.False(metric.IsHealthy);
            Assert.Contains("Insufficient Storage", metric.LastErrorMessage);

            // Act - Resolve storage headroom
            mockStorage.FreeBytes = 1024 * 1024 * 50; // 50MB > 10MB
            var metricHealthy = await monitor.EvaluateHealthAsync();

            // Assert
            Assert.True(metricHealthy.IsHealthy);
            Assert.Equal("System healthy.", metricHealthy.LastErrorMessage);
        }

        [Fact]
        public async Task DiagnosticReporter_GenerateDiagnosticReport_ShouldCompileCleanValidJson()
        {
            // Arrange
            await InitializeDatabaseAsync();
            var mockStorage = new MockStorageQuotaManager(1024 * 1024 * 100, 1024 * 1024 * 50);
            var historyRepo = new UpdateHistoryRepository(_storageOptions);
            var mockDownloader = new MockDownloadManager();
            var mockInstaller = new MockInstallerEngine();
            var mockRollback = new MockRollbackEngine();

            var monitor = new HealthMonitor(
                mockStorage,
                historyRepo,
                mockDownloader,
                mockInstaller,
                mockRollback,
                _monitoringOptions,
                NullLogger<HealthMonitor>.Instance);

            // Populate some update history to summarize
            var record1 = new UpdateHistoryRecord { Version = "1.0.0", Status = "COMPLETED", InstallationTime = DateTime.UtcNow.AddDays(-1) };
            var record2 = new UpdateHistoryRecord { Version = "1.1.0", Status = "FAILED", ErrorCode = "ERR_DISK_LOCK", Result = "Write file locked exception.", InstallationTime = DateTime.UtcNow };
            await historyRepo.InsertAsync(record1);
            await historyRepo.InsertAsync(record2);

            var reporter = new DiagnosticReporter(monitor, historyRepo);

            // Act
            string reportJson = await reporter.GenerateDiagnosticReportAsync();

            // Assert
            Assert.NotNull(reportJson);
            using var doc = JsonDocument.Parse(reportJson);
            var root = doc.RootElement;

            Assert.Equal("1.1.0", root.GetProperty("CurrentVersion").GetString());
            Assert.True(root.GetProperty("HealthStatus").GetProperty("IsHealthy").GetBoolean());

            var historySummary = root.GetProperty("UpdateHistorySummary");
            Assert.Equal(2, historySummary.GetProperty("TotalUpdateAttempts").GetInt32());
            Assert.Equal(1, historySummary.GetProperty("SuccessfulCount").GetInt32());
            Assert.Equal(1, historySummary.GetProperty("FailedCount").GetInt32());

            var failureDiagnostics = root.GetProperty("FailureDiagnostics");
            Assert.Equal("1.1.0", failureDiagnostics.GetProperty("LastFailureVersion").GetString());
            Assert.Equal("ERR_DISK_LOCK", failureDiagnostics.GetProperty("LastFailureErrorCode").GetString());
        }

        #endregion

        #region Concurrency & Cancellation Handling Tests

        [Fact]
        public async Task TelemetryReporter_ConcurrentWrites_ShouldBeThreadSafeAndPerformant()
        {
            // Arrange
            await InitializeDatabaseAsync();
            var queue = new TelemetryOfflineQueue(_storageOptions, _telemetryOptions);
            var adminClient = new AdminIntegrationClient { SimulateNetworkFailure = true }; // Keep in queue to count
            using var reporter = new TelemetryReporter(queue, adminClient, NullLogger<TelemetryReporter>.Instance, _telemetryOptions, _reportingOptions);

            int writeCount = 50;
            var tasks = new List<Task>();

            // Act - Concurrently record 50 events
            for (int i = 0; i < writeCount; i++)
            {
                int index = i;
                tasks.Add(reporter.RecordEventAsync(
                    eventType: $"ConcurrentEvent_{index}",
                    correlationId: $"corr-concurrent-{index}",
                    sourceVersion: "1.0",
                    targetVersion: "2.0",
                    success: true));
            }

            await Task.WhenAll(tasks);
            // Give the background enqueuing loop a brief moment to write physical records
            await Task.Delay(500);

            // Assert
            int finalCount = await queue.GetCountAsync();
            // Since QueueLimit is 5, it must hold exactly 5 entries and successfully evicted the rest,
            // proving that concurrency, limits, and database locks did not crash or corrupt the pipeline!
            Assert.Equal(5, finalCount);
        }

        [Fact]
        public async Task TelemetryReporter_WithCanceledToken_ShouldAbortOperationsGracefully()
        {
            // Arrange
            await InitializeDatabaseAsync();
            var queue = new TelemetryOfflineQueue(_storageOptions, _telemetryOptions);
            var adminClient = new AdminIntegrationClient();
            using var reporter = new TelemetryReporter(queue, adminClient, NullLogger<TelemetryReporter>.Instance, _telemetryOptions, _reportingOptions);

            var canceledSource = new CancellationTokenSource();
            canceledSource.Cancel();

            // Act & Assert - Ensure flush or operations with cancelled token abort immediately without crashing
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            {
                await reporter.FlushAsync(canceledSource.Token);
            });
        }

        #endregion

        #region Mock Implementations

        private class MockStorageQuotaManager : IStorageQuotaManager
        {
            public long TotalBytes { get; set; }
            public long FreeBytes { get; set; }

            public MockStorageQuotaManager(long total, long free)
            {
                TotalBytes = total;
                FreeBytes = free;
            }

            public Task<bool> HasEnoughSpaceForPackageAsync(long packageSizeBytes, CancellationToken cancellationToken = default)
            {
                return Task.FromResult(FreeBytes >= packageSizeBytes);
            }

            public Task<StorageStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default)
            {
                return Task.FromResult(new StorageStatistics
                {
                    TotalDiskSpaceBytes = TotalBytes,
                    AvailableFreeSpaceBytes = FreeBytes,
                    CacheLimitBytes = 2 * 1024 * 1024,
                    CurrentCacheSizeBytes = 0,
                    ReservedRollbackSpaceBytes = 1 * 1024 * 1024
                });
            }
        }

        private class MockDownloadManager : IDownloadManager
        {
            #pragma warning disable CS0067
            public event EventHandler<DownloadProgress> ProgressChanged;
            #pragma warning restore CS0067

            public Task<string> DownloadAsync(UpdatePackage package, CancellationToken cancellationToken = default)
            {
                return Task.FromResult("dummy_path");
            }

            public void ConfigureBandwidthPolicy(BandwidthPolicy policy) { }
        }

        private class MockInstallerEngine : IInstallerEngine
        {
            public Task<bool> InstallAsync(UpdatePackage package, CancellationToken cancellationToken = default)
            {
                return Task.FromResult(true);
            }
        }

        private class MockRollbackEngine : IRollbackEngine
        {
            public Task<bool> RollbackAsync(RollbackRecord record, CancellationToken cancellationToken = default)
            {
                return Task.FromResult(true);
            }

            public Task<bool> CreateSnapshotAsync(string snapshotId, CancellationToken cancellationToken = default) => Task.FromResult(true);
            public Task<bool> ExecuteRollbackAsync(string snapshotId, string failureReason, CancellationToken cancellationToken = default) => Task.FromResult(true);
            public Task<bool> ValidateRollbackSucceededAsync(string snapshotId, CancellationToken cancellationToken = default) => Task.FromResult(true);
        }

        #endregion
    }
}
