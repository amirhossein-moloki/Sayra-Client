using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using SayraClient.Services;
using SayraClient.Services.Recovery;
using Sayra.Client.Shared.Interfaces;
using Sayra.Client.Shared.Interfaces.Recovery;
using Sayra.Client.Shared.Models;
using Sayra.Client.Shared.Models.Recovery;
using Sayra.Client.Shared.Models.Recovery.Events;
using Sayra.Client.Shared.UpdatePlatform.Application.Interfaces;
using Sayra.Client.Shared.UpdatePlatform.Domain.Models;
using Sayra.Client.OfflineQueue;
using Sayra.Client.OfflineQueue.Models;
using Xunit;

namespace Sayra.Client.Configuration.Tests
{
    [Collection("Stage7Tests")]
    public class CrashRecoveryTests : IDisposable
    {
        private readonly ServiceCollection _services;
        private readonly Mock<ILogger<CrashRecoveryManager>> _loggerMock = new();
        private readonly Mock<ILocalDatabaseService> _dbMock = new();
        private readonly Mock<IPolicyEngine> _policyEngineMock = new();
        private readonly Mock<IPolicyRepository> _policyRepoMock = new();
        private readonly Mock<IAdDownloadManager> _downloadMock = new();
        private readonly Mock<IAdvertisementRepository> _adRepoMock = new();
        private readonly Mock<IAdvertisementCache> _cacheMock = new();
        private readonly Mock<IWorkstationSyncService> _syncMock = new();
        private readonly Mock<IOfflineQueueManager> _queueMock = new();
        private readonly Mock<IEventDispatcher> _eventDispatcherMock = new();
        private readonly Mock<IUpdateHistoryRepository> _updateHistoryMock = new();
        private readonly Mock<IRollbackEngine> _rollbackMock = new();

        private readonly string _testStatePath = Path.Combine(AppContext.BaseDirectory, "Data", "shutdown_state.json");

        public CrashRecoveryTests()
        {
            _services = new ServiceCollection();

            // Register Mock Services
            _services.AddSingleton(_loggerMock.Object);
            _services.AddSingleton(_dbMock.Object);
            _services.AddSingleton(_policyEngineMock.Object);
            _services.AddSingleton(_policyRepoMock.Object);
            _services.AddSingleton(_downloadMock.Object);
            _services.AddSingleton(_adRepoMock.Object);
            _services.AddSingleton(_cacheMock.Object);
            _services.AddSingleton(_syncMock.Object);
            _services.AddSingleton(_queueMock.Object);
            _services.AddSingleton(_eventDispatcherMock.Object);
            _services.AddSingleton(_updateHistoryMock.Object);
            _services.AddSingleton(_rollbackMock.Object);

            CleanupStateFile();
        }

        public void Dispose()
        {
            CleanupStateFile();
        }

        private void CleanupStateFile()
        {
            try
            {
                if (File.Exists(_testStatePath))
                {
                    File.Delete(_testStatePath);
                }
                string tempJpg = Path.Combine(AppContext.BaseDirectory, "test_resume.jpg");
                if (File.Exists(tempJpg)) File.Delete(tempJpg);
                string tempTmp = Path.Combine(AppContext.BaseDirectory, "test_resume.jpg.tmp");
                if (File.Exists(tempTmp)) File.Delete(tempTmp);
            }
            catch
            {
                // ignore
            }
        }

        [Fact]
        public async Task Test_NormalShutdown_RecordedAndDetected()
        {
            var provider = _services.BuildServiceProvider();
            var manager = new CrashRecoveryManager(_loggerMock.Object, provider);

            // Record clean shutdown
            await manager.RecordCleanShutdownAsync();

            // Validate
            var state = await manager.ValidatePreviousShutdownAsync();

            Assert.False(state.IsRecoveryRequired);
            Assert.Equal("Normal", state.LastShutdownReason);
        }

        [Fact]
        public async Task Test_CrashShutdown_Detected()
        {
            var provider = _services.BuildServiceProvider();
            var manager = new CrashRecoveryManager(_loggerMock.Object, provider);

            // Simulating application currently running/crashed state by writing "Running"
            var initialState = new PreviousShutdownState
            {
                LastShutdownReason = "Running",
                IsRecoveryRequired = true
            };
            string? dir = Path.GetDirectoryName(_testStatePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(_testStatePath, System.Text.Json.JsonSerializer.Serialize(initialState));

            // Validate previous shutdown state
            var state = await manager.ValidatePreviousShutdownAsync();

            Assert.True(state.IsRecoveryRequired);
            Assert.Equal("Crash", state.LastShutdownReason);
        }

        [Fact]
        public async Task Test_OfflineQueueRecovery_CorruptedAndRecreated()
        {
            _queueMock.Setup(q => q.VerifyIntegrityAsync()).ReturnsAsync(false);
            _queueMock.Setup(q => q.ForceRecreateDatabaseAsync()).Returns(Task.CompletedTask);

            var provider = _services.BuildServiceProvider();
            var manager = new CrashRecoveryManager(_loggerMock.Object, provider);

            var result = await manager.RecoverSubsystemStateAsync("OfflineQueue");

            Assert.True(result.IsSuccessful);
            Assert.Equal(RecoveryStatus.Success, result.FinalStatus);
            _queueMock.Verify(q => q.ForceRecreateDatabaseAsync(), Times.Once);
        }

        [Fact]
        public async Task Test_OfflineQueueRecovery_Success()
        {
            _queueMock.Setup(q => q.VerifyIntegrityAsync()).ReturnsAsync(true);
            _queueMock.Setup(q => q.GetPendingEventsAsync(It.IsAny<int>())).ReturnsAsync(new List<QueueItem>
            {
                new QueueItem { Id = 1, Payload = "Event" }
            });

            var provider = _services.BuildServiceProvider();
            var manager = new CrashRecoveryManager(_loggerMock.Object, provider);

            var result = await manager.RecoverSubsystemStateAsync("OfflineQueue");

            Assert.True(result.IsSuccessful);
            Assert.Contains("Recovered 1 pending items", result.OutputMessage);
            _queueMock.Verify(q => q.GetPendingEventsAsync(It.IsAny<int>()), Times.Once);
        }

        [Fact]
        public async Task Test_InterruptedDownloadRecovery_Resumes()
        {
            var campaign = new AdCampaign
            {
                CampaignId = "CAM-1",
                IsDownloaded = false,
                MediaLocalPath = Path.Combine(AppContext.BaseDirectory, "test_resume.jpg")
            };

            _adRepoMock.Setup(r => r.GetActiveCampaignsAsync(It.IsAny<CancellationToken>()))
                       .ReturnsAsync(new List<AdCampaign> { campaign });

            // Create temporary file to trigger resume download
            string tempFile = campaign.MediaLocalPath + ".tmp";
            string? dir = Path.GetDirectoryName(tempFile);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(tempFile, "partial data");

            _downloadMock.Setup(d => d.ResumeDownloadAsync(campaign, tempFile, It.IsAny<CancellationToken>()))
                         .ReturnsAsync(true);
            _downloadMock.Setup(d => d.CleanupOrphanDownloadsAsync(It.IsAny<CancellationToken>()))
                         .Returns(Task.CompletedTask);

            var provider = _services.BuildServiceProvider();
            var manager = new CrashRecoveryManager(_loggerMock.Object, provider);

            var result = await manager.RecoverSubsystemStateAsync("Downloads");

            Assert.True(result.IsSuccessful);
            _downloadMock.Verify(d => d.ResumeDownloadAsync(campaign, tempFile, It.IsAny<CancellationToken>()), Times.Once);
            _downloadMock.Verify(d => d.CleanupOrphanDownloadsAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Task_InterruptedUpdateRecovery_TriggersRollback()
        {
            var record = new UpdateHistoryRecord
            {
                Id = Guid.NewGuid(),
                Version = "1.2.0",
                Status = "STAGED"
            };

            _updateHistoryMock.Setup(u => u.GetAllAsync(It.IsAny<CancellationToken>()))
                              .ReturnsAsync(new List<UpdateHistoryRecord> { record });
            _rollbackMock.Setup(r => r.ExecuteRollbackAsync(record.Id.ToString(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                         .ReturnsAsync(true);
            _updateHistoryMock.Setup(u => u.UpdateAsync(record, It.IsAny<CancellationToken>()))
                              .Returns(Task.CompletedTask);

            var provider = _services.BuildServiceProvider();
            var manager = new CrashRecoveryManager(_loggerMock.Object, provider);

            var result = await manager.RecoverSubsystemStateAsync("Updates");

            Assert.True(result.IsSuccessful);
            Assert.Equal("ROLLED_BACK", record.Status);
            _rollbackMock.Verify(r => r.ExecuteRollbackAsync(record.Id.ToString(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
            _updateHistoryMock.Verify(u => u.UpdateAsync(record, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Test_CacheCleanup_Success()
        {
            _cacheMock.Setup(c => c.ClearExpiredCacheAsync(It.IsAny<CancellationToken>()))
                      .Returns(Task.CompletedTask);

            var provider = _services.BuildServiceProvider();
            var manager = new CrashRecoveryManager(_loggerMock.Object, provider);

            var result = await manager.RecoverSubsystemStateAsync("Cache");

            Assert.True(result.IsSuccessful);
            _cacheMock.Verify(c => c.ClearExpiredCacheAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Test_SyncRecovery_Success()
        {
            _syncMock.Setup(s => s.CompareLocalAndServerAsync(It.IsAny<CancellationToken>()))
                     .ReturnsAsync(new SyncDelta { CalculatedAt = DateTime.UtcNow });

            var provider = _services.BuildServiceProvider();
            var manager = new CrashRecoveryManager(_loggerMock.Object, provider);

            var result = await manager.RecoverSubsystemStateAsync("Sync");

            Assert.True(result.IsSuccessful);
            _syncMock.Verify(s => s.CompareLocalAndServerAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Test_PolicyStateRecovery_Success()
        {
            var profile = new PolicyProfile { PolicyId = "POL-SAFE" };
            _policyRepoMock.Setup(r => r.GetActivePoliciesAsync(It.IsAny<CancellationToken>()))
                           .ReturnsAsync(new List<PolicyProfile> { profile });
            _policyEngineMock.Setup(e => e.ApplyPoliciesAsync(profile, It.IsAny<CancellationToken>()))
                             .ReturnsAsync(new PolicyChangeResult { Success = true });

            var provider = _services.BuildServiceProvider();
            var manager = new CrashRecoveryManager(_loggerMock.Object, provider);

            var result = await manager.RecoverSubsystemStateAsync("Policy");

            Assert.True(result.IsSuccessful);
            _policyEngineMock.Verify(e => e.ApplyPoliciesAsync(profile, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Test_DatabaseValidation_Success()
        {
            // Use Microsoft.Data.Sqlite in-memory connection
            using var connection = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:;");
            await connection.OpenAsync();

            _dbMock.Setup(d => d.InitializeDatabaseAsync(It.IsAny<CancellationToken>()))
                   .Returns(Task.CompletedTask);
            _dbMock.Setup(d => d.CreateConnection())
                   .Returns(connection);

            var provider = _services.BuildServiceProvider();
            var manager = new CrashRecoveryManager(_loggerMock.Object, provider);

            await manager.VerifyAndRepairDatabaseAsync();

            _dbMock.Verify(d => d.InitializeDatabaseAsync(It.IsAny<CancellationToken>()), Times.Once);
            _dbMock.Verify(d => d.CreateConnection(), Times.Once);
        }

        [Fact]
        public async Task Test_ExecuteStartupRecovery_AbnormalShutdown_OrchestratesAll()
        {
            // Setup dirty shutdown file
            var initialState = new PreviousShutdownState
            {
                LastShutdownReason = "Running",
                IsRecoveryRequired = true
            };
            string? dir = Path.GetDirectoryName(_testStatePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(_testStatePath, System.Text.Json.JsonSerializer.Serialize(initialState));

            _queueMock.Setup(q => q.VerifyIntegrityAsync()).ReturnsAsync(true);
            _queueMock.Setup(q => q.GetPendingEventsAsync(It.IsAny<int>())).ReturnsAsync(new List<QueueItem>());

            var provider = _services.BuildServiceProvider();
            var manager = new CrashRecoveryManager(_loggerMock.Object, provider);

            await manager.ExecuteStartupRecoveryAsync();

            _eventDispatcherMock.Verify(e => e.Dispatch(It.IsAny<CrashRecoveryStartedEvent>()), Times.Once);
            _eventDispatcherMock.Verify(e => e.Dispatch(It.IsAny<CrashRecoveryCompletedEvent>()), Times.Once);
        }

        [Fact]
        public async Task Test_ExecuteStartupRecovery_NormalShutdown_NoRecovery()
        {
            var provider = _services.BuildServiceProvider();
            var manager = new CrashRecoveryManager(_loggerMock.Object, provider);

            await manager.RecordCleanShutdownAsync();

            await manager.ExecuteStartupRecoveryAsync();

            _eventDispatcherMock.Verify(e => e.Dispatch(It.IsAny<CrashRecoveryStartedEvent>()), Times.Once);
            _eventDispatcherMock.Verify(e => e.Dispatch(It.IsAny<CrashRecoveryCompletedEvent>()), Times.Once);
            // No offline queue checking because normal shutdown doesn't execute crash recovery
            _queueMock.Verify(q => q.VerifyIntegrityAsync(), Times.Never);
        }

        [Fact]
        public async Task Test_ExecuteStartupRecovery_Idempotency()
        {
            var provider = _services.BuildServiceProvider();
            var manager = new CrashRecoveryManager(_loggerMock.Object, provider);

            await manager.RecordCleanShutdownAsync();

            // First run
            await manager.ExecuteStartupRecoveryAsync();

            // Second run
            await manager.ExecuteStartupRecoveryAsync();

            // Starts dispatch once since the execution block gates multiple runs
            _eventDispatcherMock.Verify(e => e.Dispatch(It.IsAny<CrashRecoveryStartedEvent>()), Times.Once);
        }

        [Fact]
        public async Task Test_ExecuteStartupRecovery_Cancellation()
        {
            var cts = new CancellationTokenSource();
            cts.Cancel();

            var provider = _services.BuildServiceProvider();
            var manager = new CrashRecoveryManager(_loggerMock.Object, provider);

            var ex = await Record.ExceptionAsync(() => manager.ExecuteStartupRecoveryAsync(cts.Token));
            Assert.True(ex is OperationCanceledException || ex == null);
        }

        [Fact]
        public async Task Test_FailureHandling_SubsystemException_DoesNotCrashEntirePipeline()
        {
            // Offline queue throws
            _queueMock.Setup(q => q.VerifyIntegrityAsync())
                      .ThrowsAsync(new InvalidOperationException("Offline queue crash test."));

            // Policy succeeds
            var profile = new PolicyProfile { PolicyId = "POL-GOOD" };
            _policyRepoMock.Setup(r => r.GetActivePoliciesAsync(It.IsAny<CancellationToken>()))
                           .ReturnsAsync(new List<PolicyProfile> { profile });
            _policyEngineMock.Setup(e => e.ApplyPoliciesAsync(profile, It.IsAny<CancellationToken>()))
                             .ReturnsAsync(new PolicyChangeResult { Success = true });

            var provider = _services.BuildServiceProvider();
            var manager = new CrashRecoveryManager(_loggerMock.Object, provider);

            // Directly run operations
            var results = await manager.RecoverInterruptedOperationsAsync();

            var offlineQueueResult = results.Find(r => r.SubsystemName == "OfflineQueue");
            var policyResult = results.Find(r => r.SubsystemName == "Policy");

            Assert.NotNull(offlineQueueResult);
            Assert.False(offlineQueueResult.IsSuccessful);
            Assert.Equal(RecoveryStatus.Failed, offlineQueueResult.FinalStatus);

            Assert.NotNull(policyResult);
            Assert.True(policyResult.IsSuccessful);
            Assert.Equal(RecoveryStatus.Success, policyResult.FinalStatus);
        }
    }
}
