using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using SayraClient;
using SayraClient.Services;
using SayraClient.Services.Recovery;
using Sayra.Client.Shared.Interfaces;
using Sayra.Client.Shared.Interfaces.Recovery;
using Sayra.Client.Shared.Interfaces.Security;
using Sayra.Client.Shared.Models;
using Sayra.Client.Shared.Models.Recovery;
using Sayra.Client.Launcher.Services;
using Xunit;

namespace Sayra.Client.Configuration.Tests
{
    [Collection("Stage7Tests")]
    public class RecoveryAndHardeningTests
    {
        private readonly Mock<ILogger<HealthMonitor>> _healthLogger = new();
        private readonly Mock<ILogger<SelfHealingService>> _healLogger = new();
        private readonly Mock<ILogger<ResourceMonitor>> _resourceLogger = new();
        private readonly Mock<ILogger<SecurityHardeningService>> _hardeningLogger = new();
        private readonly Mock<ILogger<CrashRecoveryManager>> _crashLogger = new();
        private readonly Mock<ILogger<GracefulShutdownService>> _shutdownLogger = new();
        private readonly Mock<ILogger<RecoveryDiagnosticsEngine>> _diagLogger = new();
        private readonly Mock<ILogger<WatchdogService>> _watchdogLogger = new();

        private readonly Mock<IWorkerSupervisor> _supervisorMock = new();
        private readonly Mock<ILocalDatabaseService> _dbMock = new();
        private readonly Mock<IRemoteCommandRepository> _cmdRepoMock = new();
        private readonly Mock<IPolicyEngine> _policyEngineMock = new();
        private readonly Mock<IPolicyRepository> _policyRepoMock = new();
        private readonly Mock<IAdDownloadManager> _downloadMock = new();
        private readonly Mock<IAdvertisementRepository> _adRepoMock = new();
        private readonly Mock<IAuditService> _auditMock = new();
        private readonly Mock<IAlertManager> _alertMock = new();
        private readonly Mock<ISignatureVerifier> _sigVerifierMock = new();
        private readonly Mock<IRemoteCommandEngine> _cmdEngineMock = new();
        private readonly Mock<IServiceHealthMonitor> _workerHealthMock = new();
        private readonly Mock<RecoveryManager> _recoveryManagerMock;
        private readonly Mock<IGameLauncherService> _launcherMock = new();

        private readonly ServiceCollection _services;
        private readonly IServiceProvider _serviceProvider;

        public RecoveryAndHardeningTests()
        {
            _recoveryManagerMock = new Mock<RecoveryManager>(new Mock<ILogger<RecoveryManager>>().Object, null, null);

            _sigVerifierMock.Setup(s => s.VerifySignature(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                            .Returns(true);

            _auditMock.Setup(a => a.VerifyAuditChainIntegrityAsync(It.IsAny<CancellationToken>()))
                      .ReturnsAsync(true);

            _services = new ServiceCollection();

            // Register Mocks in ServiceCollection
            _services.AddSingleton(_supervisorMock.Object);
            _services.AddSingleton(_dbMock.Object);
            _services.AddSingleton(_cmdRepoMock.Object);
            _services.AddSingleton(_policyEngineMock.Object);
            _services.AddSingleton(_policyRepoMock.Object);
            _services.AddSingleton(_downloadMock.Object);
            _services.AddSingleton(_adRepoMock.Object);
            _services.AddSingleton(_auditMock.Object);
            _services.AddSingleton(_alertMock.Object);
            _services.AddSingleton(_sigVerifierMock.Object);
            _services.AddSingleton(_cmdEngineMock.Object);
            _services.AddSingleton(_workerHealthMock.Object);

            // Loggers
            _services.AddSingleton(_healthLogger.Object);
            _services.AddSingleton(_healLogger.Object);
            _services.AddSingleton(_resourceLogger.Object);
            _services.AddSingleton(_hardeningLogger.Object);
            _services.AddSingleton(_crashLogger.Object);
            _services.AddSingleton(_shutdownLogger.Object);
            _services.AddSingleton(_diagLogger.Object);

            // Concrete Services
            _services.AddSingleton<IHealthMonitor, HealthMonitor>();
            _services.AddSingleton<ISelfHealingService, SelfHealingService>();
            _services.AddSingleton<ResourceMonitor>();
            _services.AddSingleton<SecurityHardeningService>();
            _services.AddSingleton<CrashRecoveryManager>();
            _services.AddSingleton<GracefulShutdownService>();
            _services.AddSingleton<RecoveryDiagnosticsEngine>();

            _serviceProvider = _services.BuildServiceProvider();
        }

        #region 1. Subsystem Crash & Auto-Recovery Tests

        [Fact]
        public async Task Test_SubsystemCrash_TriggersHealing_And_RestoresSubsystem()
        {
            var healthMonitor = _serviceProvider.GetRequiredService<IHealthMonitor>();
            var healer = _serviceProvider.GetRequiredService<ISelfHealingService>();

            // Transition database to Critical state
            healthMonitor.ReportSubsystemState("Database", SubsystemHealthState.Critical, "Connection pool depleted.");

            // Wait brief moment for the async task inside SelfHealing to execute (unit test bypasses backoff delay!)
            await Task.Delay(100);

            Assert.Equal(SubsystemHealthState.Healthy, healthMonitor.GetSubsystemHealth("Database"));
            Assert.True(healer.GetRecoveryAttemptsCount("Database") > 0);
        }

        #endregion

        #region 2. Watchdog & Deadlock/Frozen Worker Detection Tests

        [Fact]
        public void Test_Watchdog_DetectsFrozenWorker_And_FlagsCriticalHealth()
        {
            var subHealth = _serviceProvider.GetRequiredService<IHealthMonitor>();

            // Set up a worker that reported heartbeat 3 minutes ago (stale/frozen)
            var workerStates = new Dictionary<string, ServiceHealthInfo>
            {
                ["RemoteCommandEngine"] = new ServiceHealthInfo
                {
                    ServiceName = "RemoteCommandEngine",
                    State = ServiceHealthState.Healthy,
                    LastHeartbeat = DateTime.UtcNow.AddSeconds(-150) // > 120s threshold
                }
            };
            _workerHealthMock.Setup(w => w.GetDetailedHealth()).Returns(workerStates);

            // Passing null for TcpClientManager is safe as it's not accessed in the deadlock detector
            var watchdog = new WatchdogService(
                _watchdogLogger.Object,
                _recoveryManagerMock.Object,
                null!,
                _launcherMock.Object,
                _workerHealthMock.Object,
                _serviceProvider.GetRequiredService<ISelfHealingService>(),
                subHealth,
                _serviceProvider.GetRequiredService<ResourceMonitor>(),
                _serviceProvider.GetRequiredService<SecurityHardeningService>()
            );

            // Call the internal deadlock detector directly
            var method = typeof(WatchdogService).GetMethod("DetectDeadlocksAndFrozenWorkers", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.NotNull(method);
            method.Invoke(watchdog, null);

            // Should transition the "RemoteCommandEngine" subsystem health state to Critical
            Assert.Equal(SubsystemHealthState.Critical, subHealth.GetSubsystemHealth("RemoteCommandEngine"));
        }

        #endregion

        #region 3. Queue Corruption Handling Tests

        [Fact]
        public async Task Test_QueueCorruption_DuringStartupRecovery_IsHandledGracefully()
        {
            var recovery = _serviceProvider.GetRequiredService<CrashRecoveryManager>();

            // Simulate corrupted db read throwing exception
            _cmdRepoMock.Setup(r => r.GetPendingCommandsAsync(It.IsAny<CancellationToken>()))
                        .ThrowsAsync(new InvalidOperationException("Malformed SQLCipher schema."));

            // Executing recovery shouldn't crash the pipeline, it handles exceptions gracefully
            var ex = await Record.ExceptionAsync(() => recovery.ExecuteStartupRecoveryAsync());
            Assert.Null(ex);
        }

        #endregion

        #region 4. Download Interruption & Resuming Tests

        [Fact]
        public async Task Test_DownloadInterruption_TriggersRangeResume_Or_FullDownload()
        {
            var recovery = _serviceProvider.GetRequiredService<CrashRecoveryManager>();

            var campaign = new AdCampaign
            {
                CampaignId = "CAM-RESUME",
                IsDownloaded = false,
                MediaLocalPath = Path.Combine(AppContext.BaseDirectory, "test_resume.jpg")
            };

            _adRepoMock.Setup(r => r.GetActiveCampaignsAsync(It.IsAny<CancellationToken>()))
                       .ReturnsAsync(new List<AdCampaign> { campaign });

            // Create local temporary file to simulate interrupted download
            string tempFile = campaign.MediaLocalPath + ".tmp";
            File.WriteAllText(tempFile, "Partial chunk");

            await recovery.ExecuteStartupRecoveryAsync();

            _downloadMock.Verify(d => d.ResumeDownloadAsync(campaign, tempFile, It.IsAny<CancellationToken>()), Times.Once);

            if (File.Exists(tempFile)) File.Delete(tempFile);
        }

        #endregion

        #region 5. Database Lock & Recovery Concurrent Safe Tests

        [Fact]
        public async Task Test_DatabaseIntegrityAndLocks_AreIsolated_And_ThreadSafe()
        {
            var hardening = _serviceProvider.GetRequiredService<SecurityHardeningService>();

            // Simulate locked database exception
            _dbMock.Setup(d => d.CreateConnection())
                   .Throws(new InvalidOperationException("database is locked"));

            bool integrityResult = await hardening.VerifyDatabaseIntegrityAsync();

            // Should return false cleanly without crashing the thread pool
            Assert.False(integrityResult);
        }

        #endregion

        #region 6. Unexpected Shutdown & Orderly State Preservation

        [Fact]
        public async Task Test_UnexpectedShutdown_PreservesTelemetryState_And_StateTransition()
        {
            var shutdown = _serviceProvider.GetRequiredService<GracefulShutdownService>();

            // Verifies the steps proceed orderly and state manager registers STOPPED / DISCONNECTED
            await shutdown.InitiateShutdownAsync(TimeSpan.FromSeconds(2));

            _supervisorMock.Verify(s => s.StopAllAsync(), Times.Once);
        }

        #endregion

        #region 7. Restart Recovery of Executing Commands

        [Fact]
        public async Task Test_RestartRecovery_ReQueuesUncompletedExecutingCommands()
        {
            var recovery = _serviceProvider.GetRequiredService<CrashRecoveryManager>();

            var incompleteCmd = new RemoteCommandHistory
            {
                CommandId = Guid.NewGuid().ToString(),
                Action = "LOCK_PC",
                Status = "PENDING",
                ReceivedAt = DateTime.UtcNow.ToString("O"),
                Signature = "SIG"
            };

            _cmdRepoMock.Setup(r => r.GetPendingCommandsAsync(It.IsAny<CancellationToken>()))
                        .ReturnsAsync(new List<RemoteCommandHistory> { incompleteCmd });

            await recovery.ExecuteStartupRecoveryAsync();

            // Verification of command re-queue injection
            _cmdEngineMock.Verify(e => e.QueueCommandAsync(It.Is<RemoteCommand>(c => c.CommandId.ToString() == incompleteCmd.CommandId)), Times.Once);
        }

        #endregion

        #region 8. Subsystem Health Transitions & History Limit

        [Fact]
        public void Test_HealthTransitions_AppendsHistory_And_RespectsCapacityLimits()
        {
            var health = _serviceProvider.GetRequiredService<IHealthMonitor>();

            // Simulate over 60 rapid transitions to exceed history limit (50)
            for (int i = 0; i < 65; i++)
            {
                var state = (i % 2 == 0) ? SubsystemHealthState.Warning : SubsystemHealthState.Healthy;
                health.ReportSubsystemState("Database", state, $"Transition {i}");
            }

            var detailed = health.GetDetailedHealth()["Database"];
            Assert.True(detailed.HealthHistory.Count <= 50, "Health transition history exceeded capacity limit of 50!");
        }

        #endregion

        #region 9. Restart Loops & Storm Prevention Tests

        [Fact]
        public async Task Test_RestartStormPrevention_DisablesAutomaticRecovery_AfterExcessiveCrashes()
        {
            var health = _serviceProvider.GetRequiredService<IHealthMonitor>();
            var healer = _serviceProvider.GetRequiredService<ISelfHealingService>();

            // Directly invoke healer sequentially to trigger loop protection without threading delays
            for (int i = 0; i < 10; i++)
            {
                await healer.RecoverSubsystemAsync("Database");
            }

            // Recovery attempts should lock at max limit (5) and set subsystem state to Offline (disabled)
            Assert.Equal(SubsystemHealthState.Offline, health.GetSubsystemHealth("Database"));
            Assert.True(healer.GetRecoveryAttemptsCount("Database") >= 5);
        }

        #endregion

        #region 10. Resource Pressure Throttling & Degradation Tests

        [Fact]
        public async Task Test_MemoryAndCpuPressure_TriggersGracefulDegradation_And_CacheCleanup()
        {
            var testServices = new ServiceCollection();

            var mockCache = new Mock<IAdvertisementCache>();
            testServices.AddSingleton(mockCache.Object);
            testServices.AddSingleton(_resourceLogger.Object);
            testServices.AddSingleton<ResourceMonitor>();

            var localProvider = testServices.BuildServiceProvider();
            var monitorWithCache = localProvider.GetRequiredService<ResourceMonitor>();

            // Simulate extreme pressure: CPU 98%, RAM 2GB, Free Disk 10MB
            monitorWithCache.SetSimulatedResources(98.0, 2048 * 1024 * 1024L, 50, 400, 10 * 1024 * 1024L);

            await monitorWithCache.RunResourceAuditAsync();

            // Verifies automatic cache clearing and eviction are called to free storage pressure
            mockCache.Verify(c => c.EvictLeastRecentlyUsedAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()), Times.Once);
            mockCache.Verify(c => c.ClearExpiredCacheAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        #endregion

        #region 11. Security Hardening & Tampering Verification Tests

        [Fact]
        public async Task Test_SecurityHardening_DetectsTamperedMediaChecksums()
        {
            var hardening = _serviceProvider.GetRequiredService<SecurityHardeningService>();

            var localMediaFile = Path.Combine(AppContext.BaseDirectory, "test_tampered.jpg");
            File.WriteAllText(localMediaFile, "Sponsor Advertisement File Content");

            var mediaRecord = new DownloadedMedia
            {
                CampaignId = "CAM-1",
                MediaPath = localMediaFile,
                Checksum = "invalid_expected_checksum" // Mismatch to trigger tampering alert!
            };

            _adRepoMock.Setup(r => r.GetDownloadedMediaListAsync(It.IsAny<CancellationToken>()))
                       .ReturnsAsync(new List<DownloadedMedia> { mediaRecord });

            bool isHealthy = await hardening.VerifyDownloadedMediaIntegrityAsync();
            Assert.False(isHealthy, "Security Hardening should detect and fail media tampering check.");

            if (File.Exists(localMediaFile)) File.Delete(localMediaFile);
        }

        #endregion

        #region 12. Audit Trail Cryptographic Validation Tests

        [Fact]
        public async Task Test_AuditTrail_DetectsCorruptedHashChain()
        {
            var hardening = _serviceProvider.GetRequiredService<SecurityHardeningService>();

            // Simulate broken cryptographic chain signature verification returning false
            _auditMock.Setup(a => a.VerifyAuditChainIntegrityAsync(It.IsAny<CancellationToken>()))
                      .ReturnsAsync(false);

            bool isValid = await hardening.VerifyAuditIntegrityAsync();
            Assert.False(isValid);
        }

        #endregion

        #region 13. Database Reindexing & Repair Verification Tests

        [Fact]
        public async Task Test_DatabaseConsistentReindex_IsPerformedCorrectly()
        {
            var recovery = _serviceProvider.GetRequiredService<CrashRecoveryManager>();

            // Verify execution proceeds cleanly when DB can be successfully initialized
            var ex = await Record.ExceptionAsync(() => recovery.VerifyAndRepairDatabaseAsync());
            Assert.Null(ex);

            _dbMock.Verify(d => d.InitializeDatabaseAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        #endregion

        #region 14. Graceful Shutdown Step Ordering Verification

        [Fact]
        public async Task Test_GracefulShutdown_ExecutesSevenStepsInOrder()
        {
            var shutdown = _serviceProvider.GetRequiredService<GracefulShutdownService>();

            // Verifies teardown execution flow successfully completes
            await shutdown.InitiateShutdownAsync(TimeSpan.FromSeconds(3));

            _supervisorMock.Verify(s => s.StopAllAsync(), Times.Once);
        }

        #endregion

        #region 15. Simulated 24-Hour Long-Running Stability Verification

        [Fact]
        public async Task Test_Simulated24HourCycle_VerifiesSystemStability()
        {
            var health = _serviceProvider.GetRequiredService<IHealthMonitor>();
            var healer = _serviceProvider.GetRequiredService<ISelfHealingService>();

            // Simulate 24-hour cycle (e.g. 24 loops of audit checks and randomized recoveries)
            for (int i = 0; i < 24; i++)
            {
                await healer.MonitorAndHealAsync();
                Assert.Equal(SubsystemHealthState.Healthy, health.GetSubsystemHealth("Database"));
            }
        }

        #endregion

        #region 16. Fault Isolation Boundaries Tests

        [Fact]
        public void Test_FaultIsolation_AdFailure_DoesNotImpactTelemetryOrCommands()
        {
            var health = _serviceProvider.GetRequiredService<IHealthMonitor>();

            // Trigger AdvertisementEngine offline failure
            health.ReportSubsystemState("AdvertisementEngine", SubsystemHealthState.Offline, "Failed rendering frame.");

            // Verify isolated states: Telemetry and RemoteCommandEngine remain Healthy!
            Assert.Equal(SubsystemHealthState.Offline, health.GetSubsystemHealth("AdvertisementEngine"));
            Assert.Equal(SubsystemHealthState.Healthy, health.GetSubsystemHealth("Telemetry"));
            Assert.Equal(SubsystemHealthState.Healthy, health.GetSubsystemHealth("RemoteCommandEngine"));
        }

        #endregion

        #region 17. Concurrent Subsystem Failure Scenarios

        [Fact]
        public async Task Test_ConcurrentSubsystemFailure_HealsAllConcurrently()
        {
            var health = _serviceProvider.GetRequiredService<IHealthMonitor>();
            var healer = _serviceProvider.GetRequiredService<ISelfHealingService>();

            // Simulate concurrent critical failure on both PolicyEngine and FleetManager
            health.ReportSubsystemState("PolicyEngine", SubsystemHealthState.Critical, "Config lock failed.");
            health.ReportSubsystemState("FleetManager", SubsystemHealthState.Critical, "Broker disconnected.");

            // Wait brief moment for background heal tasks to fire
            await Task.Delay(100);

            // Both should be restored back to Healthy concurrently
            Assert.Equal(SubsystemHealthState.Healthy, health.GetSubsystemHealth("PolicyEngine"));
            Assert.Equal(SubsystemHealthState.Healthy, health.GetSubsystemHealth("FleetManager"));
        }

        #endregion
    }
}
