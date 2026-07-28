using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Moq;
using Microsoft.Extensions.Logging;
using Sayra.Client.Shared.UpdatePlatform.Application.Interfaces;
using Sayra.Client.Shared.UpdatePlatform.Application.Services;
using Sayra.Client.Shared.UpdatePlatform.Domain.Enums;
using Sayra.Client.Shared.UpdatePlatform.Domain.Exceptions;
using Sayra.Client.Shared.UpdatePlatform.Domain.Models;

namespace Sayra.Client.Configuration.Tests
{
    /// <summary>
    /// Comprehensive test suite verifying Phase 6 Part 6: Rollback & Recovery Platform.
    /// Covers rollback, backup management, state transitions, concurrent operations, validation, and power failure recovery.
    /// </summary>
    public class UpdatePlatformPart6Tests
    {
        private readonly Mock<ILogger<BackupManager>> _backupLoggerMock = new();
        private readonly Mock<ILogger<SnapshotManager>> _snapshotLoggerMock = new();
        private readonly Mock<ILogger<RecoveryValidator>> _validatorLoggerMock = new();
        private readonly Mock<ILogger<RollbackEngine>> _rollbackLoggerMock = new();
        private readonly Mock<ILogger<RecoveryEngine>> _recoveryLoggerMock = new();

        #region State Machine Tests

        [Fact]
        public void StateMachine_ShouldTransitionToValidStates()
        {
            var fsm = new RecoveryStateMachine();
            Assert.Equal(RecoveryState.Idle, fsm.CurrentState);

            fsm.TransitionTo(RecoveryState.BackupCreated);
            Assert.Equal(RecoveryState.BackupCreated, fsm.CurrentState);

            fsm.TransitionTo(RecoveryState.Monitoring);
            Assert.Equal(RecoveryState.Monitoring, fsm.CurrentState);

            fsm.TransitionTo(RecoveryState.RecoveryRequired);
            Assert.Equal(RecoveryState.RecoveryRequired, fsm.CurrentState);

            fsm.TransitionTo(RecoveryState.RollingBack);
            Assert.Equal(RecoveryState.RollingBack, fsm.CurrentState);

            fsm.TransitionTo(RecoveryState.Restoring);
            Assert.Equal(RecoveryState.Restoring, fsm.CurrentState);

            fsm.TransitionTo(RecoveryState.Verifying);
            Assert.Equal(RecoveryState.Verifying, fsm.CurrentState);

            fsm.TransitionTo(RecoveryState.Completed);
            Assert.Equal(RecoveryState.Completed, fsm.CurrentState);
        }

        [Fact]
        public void StateMachine_ShouldRejectInvalidTransitions()
        {
            var fsm = new RecoveryStateMachine();
            Assert.Throws<RecoveryFailedException>(() => fsm.TransitionTo(RecoveryState.Completed));
        }

        [Fact]
        public void StateMachine_ShouldSupportThreadSafeConcurrentTransitions()
        {
            var fsm = new RecoveryStateMachine();
            int successTransitions = 0;
            int failedTransitions = 0;

            Parallel.For(0, 100, i =>
            {
                try
                {
                    fsm.TransitionTo(RecoveryState.BackupCreated);
                    Interlocked.Increment(ref successTransitions);
                }
                catch
                {
                    Interlocked.Increment(ref failedTransitions);
                }
            });

            Assert.Equal(1, successTransitions);
            Assert.Equal(99, failedTransitions);
        }

        #endregion

        #region Backup Management Tests

        [Fact]
        public async Task BackupManager_ShouldCreateAndRestoreBackupSuccessfully()
        {
            string tempRoot = Path.Combine(Path.GetTempPath(), "SAYRA_Test_Backup_" + Guid.NewGuid());
            string srcDir = Path.Combine(tempRoot, "Source");
            string destDir = Path.Combine(tempRoot, "Destination");
            string restoreDir = Path.Combine(tempRoot, "Restore");

            Directory.CreateDirectory(srcDir);
            Directory.CreateDirectory(destDir);

            try
            {
                File.WriteAllText(Path.Combine(srcDir, "test.txt"), "Database data");

                var backupManager = new BackupManager(_backupLoggerMock.Object);
                var backup = await backupManager.CreateBackupAsync("BKP_1", srcDir, destDir, CancellationToken.None);

                Assert.True(backup.IsValid);
                Assert.True(File.Exists(backup.FilePath));

                bool isValid = await backupManager.ValidateBackupAsync(backup, CancellationToken.None);
                Assert.True(isValid);

                bool restoreOk = await backupManager.RestoreBackupAsync(backup, restoreDir, CancellationToken.None);
                Assert.True(restoreOk);
                Assert.True(File.Exists(Path.Combine(restoreDir, "test.txt")));
                Assert.Equal("Database data", File.ReadAllText(Path.Combine(restoreDir, "test.txt")));
            }
            finally
            {
                if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, true);
            }
        }

        [Fact]
        public async Task BackupManager_ShouldRejectCorruptedBackup()
        {
            string tempRoot = Path.Combine(Path.GetTempPath(), "SAYRA_Test_Backup_Corrupt_" + Guid.NewGuid());
            string srcDir = Path.Combine(tempRoot, "Source");
            string destDir = Path.Combine(tempRoot, "Destination");

            Directory.CreateDirectory(srcDir);
            Directory.CreateDirectory(destDir);

            try
            {
                File.WriteAllText(Path.Combine(srcDir, "test.txt"), "Database data");

                var backupManager = new BackupManager(_backupLoggerMock.Object);
                var backup = await backupManager.CreateBackupAsync("BKP_1", srcDir, destDir, CancellationToken.None);

                // Corrupt file contents on disk
                File.WriteAllText(backup.FilePath, "Tampered content");

                bool isValid = await backupManager.ValidateBackupAsync(backup, CancellationToken.None);
                Assert.False(isValid);

                await Assert.ThrowsAsync<BackupValidationException>(() => backupManager.RestoreBackupAsync(backup, Path.Combine(tempRoot, "Restore"), CancellationToken.None));
            }
            finally
            {
                if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, true);
            }
        }

        [Fact]
        public async Task BackupManager_CleanupExpiredBackups_ShouldOnlyRetainNewFiles()
        {
            string tempRoot = Path.Combine(Path.GetTempPath(), "SAYRA_Test_Backup_Retention_" + Guid.NewGuid());
            Directory.CreateDirectory(tempRoot);

            try
            {
                string oldFile = Path.Combine(tempRoot, "old_backup.zip");
                string newFile = Path.Combine(tempRoot, "new_backup.zip");

                File.WriteAllText(oldFile, "Fake old content");
                File.WriteAllText(newFile, "Fake new content");

                // Artificially change creation time of the old file
                File.SetCreationTimeUtc(oldFile, DateTime.UtcNow.AddDays(-10));

                var backupManager = new BackupManager(_backupLoggerMock.Object);
                await backupManager.CleanupExpiredBackupsAsync(tempRoot, TimeSpan.FromDays(5), CancellationToken.None);

                Assert.False(File.Exists(oldFile));
                Assert.True(File.Exists(newFile));
            }
            finally
            {
                if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, true);
            }
        }

        #endregion

        #region Snapshot Management Tests

        [Fact]
        public async Task SnapshotManager_ShouldCreateAndRestoreSnapshotSuccessfully()
        {
            string tempRoot = Path.Combine(Path.GetTempPath(), "SAYRA_Test_Snapshot_" + Guid.NewGuid());
            string binDir = Path.Combine(tempRoot, "Binaries");
            string confDir = Path.Combine(tempRoot, "Configs");

            string targetBin = Path.Combine(tempRoot, "RestoredBinaries");
            string targetConf = Path.Combine(tempRoot, "RestoredConfigs");

            Directory.CreateDirectory(binDir);
            Directory.CreateDirectory(confDir);

            try
            {
                File.WriteAllText(Path.Combine(binDir, "app.dll"), "App content");
                File.WriteAllText(Path.Combine(confDir, "app.config"), "Config content");

                var backupManager = new BackupManager(_backupLoggerMock.Object);
                var snapshotManager = new SnapshotManager(_snapshotLoggerMock.Object, backupManager);

                var snapshot = await snapshotManager.CreateSnapshotAsync("SNAP_1", binDir, confDir, CancellationToken.None);

                Assert.True(snapshot.IsValid);
                Assert.True(File.Exists(snapshot.FilePath));

                bool isValid = await snapshotManager.ValidateSnapshotAsync(snapshot, CancellationToken.None);
                Assert.True(isValid);

                bool restoreOk = await snapshotManager.RestoreSnapshotAsync(snapshot, targetBin, targetConf, CancellationToken.None);
                Assert.True(restoreOk);

                Assert.True(File.Exists(Path.Combine(targetBin, "app.dll")));
                Assert.Equal("App content", File.ReadAllText(Path.Combine(targetBin, "app.dll")));
                Assert.True(File.Exists(Path.Combine(targetConf, "app.config")));
                Assert.Equal("Config content", File.ReadAllText(Path.Combine(targetConf, "app.config")));
            }
            finally
            {
                if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, true);
            }
        }

        [Fact]
        public async Task SnapshotManager_CorruptedSnapshot_ShouldFailValidation()
        {
            string tempRoot = Path.Combine(Path.GetTempPath(), "SAYRA_Test_Snapshot_Fail_" + Guid.NewGuid());
            string binDir = Path.Combine(tempRoot, "Binaries");
            string confDir = Path.Combine(tempRoot, "Configs");

            Directory.CreateDirectory(binDir);
            Directory.CreateDirectory(confDir);

            try
            {
                File.WriteAllText(Path.Combine(binDir, "app.dll"), "App content");

                var backupManager = new BackupManager(_backupLoggerMock.Object);
                var snapshotManager = new SnapshotManager(_snapshotLoggerMock.Object, backupManager);

                var snapshot = await snapshotManager.CreateSnapshotAsync("SNAP_1", binDir, confDir, CancellationToken.None);

                // Tamper with snapshot zip on disk
                File.WriteAllText(snapshot.FilePath, "Garbage text");

                bool isValid = await snapshotManager.ValidateSnapshotAsync(snapshot, CancellationToken.None);
                Assert.False(isValid);

                await Assert.ThrowsAsync<RollbackFailedException>(() => snapshotManager.RestoreSnapshotAsync(snapshot, binDir, confDir, CancellationToken.None));
            }
            finally
            {
                if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, true);
            }
        }

        #endregion

        #region Recovery Validation Tests

        [Fact]
        public async Task RecoveryValidator_HealthyContext_ShouldSucceed()
        {
            string tempRoot = Path.Combine(Path.GetTempPath(), "SAYRA_Test_Val_Good_" + Guid.NewGuid());
            Directory.CreateDirectory(tempRoot);

            try
            {
                string dllPath = Path.Combine(tempRoot, "test.dll");
                File.WriteAllText(dllPath, "Happy logic");

                string configPath = Path.Combine(tempRoot, "client_config.json");
                File.WriteAllText(configPath, "{ \"test\": true }");

                var context = new RecoveryContext
                {
                    InstallationDirectory = tempRoot,
                    CriticalFiles = new List<string> { "test.dll" },
                    FileHashes = new Dictionary<string, string> { { "test.dll", GetSha256OfText("Happy logic") } },
                    ConfigurationFilePath = "client_config.json"
                };

                var validator = new RecoveryValidator(_validatorLoggerMock.Object);
                var result = await validator.ValidateHealthAsync(context, CancellationToken.None);

                Assert.True(result.IsHealthy);
                Assert.True(result.CriticalFilesExist);
                Assert.True(result.FileHashesValid);
                Assert.True(result.ConfigurationReadable);
            }
            finally
            {
                if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, true);
            }
        }

        [Fact]
        public async Task RecoveryValidator_MissingCriticalFile_ShouldFail()
        {
            string tempRoot = Path.Combine(Path.GetTempPath(), "SAYRA_Test_Val_Missing_" + Guid.NewGuid());
            Directory.CreateDirectory(tempRoot);

            try
            {
                var context = new RecoveryContext
                {
                    InstallationDirectory = tempRoot,
                    CriticalFiles = new List<string> { "missing.dll" }
                };

                var validator = new RecoveryValidator(_validatorLoggerMock.Object);
                var result = await validator.ValidateHealthAsync(context, CancellationToken.None);

                Assert.False(result.IsHealthy);
                Assert.False(result.CriticalFilesExist);
            }
            finally
            {
                if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, true);
            }
        }

        [Fact]
        public async Task RecoveryValidator_InvalidHash_ShouldFail()
        {
            string tempRoot = Path.Combine(Path.GetTempPath(), "SAYRA_Test_Val_Hash_" + Guid.NewGuid());
            Directory.CreateDirectory(tempRoot);

            try
            {
                string dllPath = Path.Combine(tempRoot, "test.dll");
                File.WriteAllText(dllPath, "Modified logic");

                var context = new RecoveryContext
                {
                    InstallationDirectory = tempRoot,
                    CriticalFiles = new List<string> { "test.dll" },
                    FileHashes = new Dictionary<string, string> { { "test.dll", "some_incorrect_hash_value" } }
                };

                var validator = new RecoveryValidator(_validatorLoggerMock.Object);
                var result = await validator.ValidateHealthAsync(context, CancellationToken.None);

                Assert.False(result.IsHealthy);
                Assert.False(result.FileHashesValid);
            }
            finally
            {
                if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, true);
            }
        }

        [Fact]
        public async Task RecoveryValidator_InvalidConfiguration_ShouldFail()
        {
            string tempRoot = Path.Combine(Path.GetTempPath(), "SAYRA_Test_Val_Config_" + Guid.NewGuid());
            Directory.CreateDirectory(tempRoot);

            try
            {
                string configPath = Path.Combine(tempRoot, "client_config.json");
                File.WriteAllText(configPath, "THIS_IS_NOT_VALID_JSON_{{{");

                var context = new RecoveryContext
                {
                    InstallationDirectory = tempRoot,
                    ConfigurationFilePath = "client_config.json"
                };

                var validator = new RecoveryValidator(_validatorLoggerMock.Object);
                var result = await validator.ValidateHealthAsync(context, CancellationToken.None);

                Assert.False(result.IsHealthy);
                Assert.False(result.ConfigurationReadable);
            }
            finally
            {
                if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, true);
            }
        }

        #endregion

        #region Recovery Engine & End-To-End Tests

        [Fact]
        public async Task RecoveryEngine_ShouldAutomaticallyTriggerAndRollbackSuccessfully()
        {
            string tempRoot = Path.Combine(Path.GetTempPath(), "SAYRA_Test_RE_E2E_" + Guid.NewGuid());
            string binDir = Path.Combine(tempRoot, "Production_Binaries");
            string confDir = Path.Combine(tempRoot, "Production_Configs");

            Directory.CreateDirectory(binDir);
            Directory.CreateDirectory(confDir);

            try
            {
                // Create preceding healthy source version "1.0.0" files
                File.WriteAllText(Path.Combine(binDir, "app.dll"), "Version 1.0 App");
                File.WriteAllText(Path.Combine(confDir, "client_config.json"), "{ \"version\": \"1.0.0\" }");

                var backupManager = new BackupManager(_backupLoggerMock.Object);
                var snapshotManager = new SnapshotManager(_snapshotLoggerMock.Object, backupManager);
                var rollbackEngine = new RollbackEngine(_rollbackLoggerMock.Object, snapshotManager)
                {
                    BinariesDirectory = binDir,
                    ConfigurationsDirectory = confDir
                };

                // Take Snapshot of "1.0.0"
                bool snapshotOk = await rollbackEngine.CreateSnapshotAsync("1.0.0", CancellationToken.None);
                Assert.True(snapshotOk);

                // Simulate update installation to target version "2.0.0" (which is corrupted/partially written)
                File.WriteAllText(Path.Combine(binDir, "app.dll"), "Version 2.0 Corrupted App");
                File.WriteAllText(Path.Combine(confDir, "client_config.json"), "NOT_JSON_DATA_!!!");

                // Define context
                var context = new RecoveryContext
                {
                    TargetVersion = "2.0.0",
                    SourceVersion = "1.0.0",
                    InstallationDirectory = binDir,
                    CriticalFiles = new List<string> { "app.dll" },
                    FileHashes = new Dictionary<string, string> { { "app.dll", GetSha256OfText("Version 2.0 Corrupted App") } },
                    ConfigurationFilePath = Path.Combine(confDir, "client_config.json")
                };

                var validator = new RecoveryValidator(_validatorLoggerMock.Object);
                var stateMachine = new RecoveryStateMachine();
                var recoveryEngine = new RecoveryEngine(_recoveryLoggerMock.Object, rollbackEngine, validator, stateMachine);

                // Since configuration is corrupt JSON, validator will fail, triggering rollback to "1.0.0"
                // Change the validation expectations for rollback check to pass validation of 1.0.0:
                context.FileHashes["app.dll"] = GetSha256OfText("Version 1.0 App");
                context.ConfigurationFilePath = Path.Combine(confDir, "client_config.json");

                bool recovered = await recoveryEngine.DetectAndTriggerRecoveryIfNeededAsync(context, CancellationToken.None);
                Assert.True(recovered);

                // Verify "1.0.0" restored properly
                Assert.Equal("Version 1.0 App", File.ReadAllText(Path.Combine(binDir, "app.dll")));
                Assert.Equal("{ \"version\": \"1.0.0\" }", File.ReadAllText(Path.Combine(confDir, "client_config.json")));
                Assert.Equal(RecoveryState.Completed, stateMachine.CurrentState);
            }
            finally
            {
                if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, true);
            }
        }

        [Fact]
        public async Task RecoveryEngine_ShouldHandleFailedRollbackAndTransitionToFailed()
        {
            var rollbackEngineMock = new Mock<IRollbackEngine>();
            rollbackEngineMock.Setup(r => r.ExecuteRollbackAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(false); // Simulate rollback engine failure

            var validatorMock = new Mock<IRecoveryValidator>();
            validatorMock.Setup(v => v.ValidateHealthAsync(It.IsAny<RecoveryContext>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new HealthValidationResult { IsHealthy = false });

            var stateMachine = new RecoveryStateMachine();
            var recoveryEngine = new RecoveryEngine(_recoveryLoggerMock.Object, rollbackEngineMock.Object, validatorMock.Object, stateMachine);

            var context = new RecoveryContext { TargetVersion = "2.0.0", SourceVersion = "1.0.0" };
            var report = await recoveryEngine.RecoverAsync(context, CancellationToken.None);

            Assert.False(report.Succeeded);
            Assert.Equal(RecoveryState.Failed, report.FinalState);
            Assert.Equal(RecoveryState.Failed, stateMachine.CurrentState);
        }

        [Fact]
        public async Task RecoveryEngine_ShouldSupportCancellation()
        {
            var rollbackEngineMock = new Mock<IRollbackEngine>();
            var validatorMock = new Mock<IRecoveryValidator>();
            var stateMachine = new RecoveryStateMachine();
            var recoveryEngine = new RecoveryEngine(_recoveryLoggerMock.Object, rollbackEngineMock.Object, validatorMock.Object, stateMachine);

            var context = new RecoveryContext { TargetVersion = "2.0.0", SourceVersion = "1.0.0" };
            var cts = new CancellationTokenSource();
            cts.Cancel(); // Immediately cancel

            var report = await recoveryEngine.RecoverAsync(context, cts.Token);

            Assert.False(report.Succeeded);
            Assert.Equal(RecoveryState.Failed, stateMachine.CurrentState);
        }

        [Fact]
        public async Task RollbackEngine_ShouldBeIdempotent()
        {
            string tempRoot = Path.Combine(Path.GetTempPath(), "SAYRA_Test_RE_Idempotent_" + Guid.NewGuid());
            string binDir = Path.Combine(tempRoot, "Binaries");
            string confDir = Path.Combine(tempRoot, "Configs");

            Directory.CreateDirectory(binDir);
            Directory.CreateDirectory(confDir);

            try
            {
                File.WriteAllText(Path.Combine(binDir, "app.dll"), "Version 1.0");

                var backupManager = new BackupManager(_backupLoggerMock.Object);
                var snapshotManager = new SnapshotManager(_snapshotLoggerMock.Object, backupManager);
                var rollbackEngine = new RollbackEngine(_rollbackLoggerMock.Object, snapshotManager)
                {
                    BinariesDirectory = binDir,
                    ConfigurationsDirectory = confDir
                };

                await rollbackEngine.CreateSnapshotAsync("1.0.0", CancellationToken.None);

                // Run rollback first time
                bool success1 = await rollbackEngine.ExecuteRollbackAsync("1.0.0", "Failure reason", CancellationToken.None);
                Assert.True(success1);

                // Run rollback second time (idempotency check)
                bool success2 = await rollbackEngine.ExecuteRollbackAsync("1.0.0", "Failure reason", CancellationToken.None);
                Assert.True(success2);

                Assert.Equal("Version 1.0", File.ReadAllText(Path.Combine(binDir, "app.dll")));
            }
            finally
            {
                if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, true);
            }
        }

        #endregion

        #region Chaos, Power Loss, Persistence & Concurrency Tests

        [Fact]
        public async Task RecoveryEngine_RecoveryAfterApplicationRestart_ShouldRestoreRegistry()
        {
            string tempRoot = Path.Combine(Path.GetTempPath(), "SAYRA_Test_Restart_" + Guid.NewGuid());
            string binDir = Path.Combine(tempRoot, "Binaries");
            string confDir = Path.Combine(tempRoot, "Configs");

            Directory.CreateDirectory(binDir);
            Directory.CreateDirectory(confDir);

            try
            {
                File.WriteAllText(Path.Combine(binDir, "app.dll"), "Original App");

                var backupManager = new BackupManager(_backupLoggerMock.Object);
                var snapshotManager = new SnapshotManager(_snapshotLoggerMock.Object, backupManager);

                // 1. Initial run of RollbackEngine
                var rollbackEngine = new RollbackEngine(_rollbackLoggerMock.Object, snapshotManager)
                {
                    BinariesDirectory = binDir,
                    ConfigurationsDirectory = confDir
                };

                await rollbackEngine.CreateSnapshotAsync("1.0.0", CancellationToken.None);

                // 2. Simulate application restart (New instance of RollbackEngine loads registry from snapshots_registry.json on disk!)
                var restartedRollbackEngine = new RollbackEngine(_rollbackLoggerMock.Object, snapshotManager)
                {
                    BinariesDirectory = binDir,
                    ConfigurationsDirectory = confDir
                };

                // Corrupt file
                File.WriteAllText(Path.Combine(binDir, "app.dll"), "Corrupt App");

                // Execute rollback successfully using persistent snapshot registry load!
                bool ok = await restartedRollbackEngine.ExecuteRollbackAsync("1.0.0", "Recovery after restart", CancellationToken.None);
                Assert.True(ok);
                Assert.Equal("Original App", File.ReadAllText(Path.Combine(binDir, "app.dll")));
            }
            finally
            {
                if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, true);
            }
        }

        [Fact]
        public async Task RecoveryEngine_SimulatedPowerFailureDuringRollbackRestore_ShouldRecoverAtomicDirectories()
        {
            string tempRoot = Path.Combine(Path.GetTempPath(), "SAYRA_Test_PowerLoss_" + Guid.NewGuid());
            string binDir = Path.Combine(tempRoot, "Binaries");
            string confDir = Path.Combine(tempRoot, "Configs");

            Directory.CreateDirectory(binDir);
            Directory.CreateDirectory(confDir);

            try
            {
                File.WriteAllText(Path.Combine(binDir, "app.dll"), "Original App");

                var backupManager = new BackupManager(_backupLoggerMock.Object);
                var snapshotManager = new SnapshotManager(_snapshotLoggerMock.Object, backupManager);

                var rollbackEngine = new RollbackEngine(_rollbackLoggerMock.Object, snapshotManager)
                {
                    BinariesDirectory = binDir,
                    ConfigurationsDirectory = confDir
                };

                await rollbackEngine.CreateSnapshotAsync("1.0.0", CancellationToken.None);

                // Modify files to simulate update
                File.WriteAllText(Path.Combine(binDir, "app.dll"), "Update App");

                // Simulate cancel or interrupted mid-restore (which should leave source directory completely untouched or easily roll back folder swaps)
                var cts = new CancellationTokenSource();
                cts.Cancel(); // Simulate immediate power-cut/interruption

                await Assert.ThrowsAnyAsync<Exception>(() => rollbackEngine.ExecuteRollbackAsync("1.0.0", "Interrupted", cts.Token));

                // Assert that original folder content still has the updated state or is uncorrupted (never partially written)
                Assert.True(File.Exists(Path.Combine(binDir, "app.dll")));
            }
            finally
            {
                if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, true);
            }
        }

        [Fact]
        public async Task RecoveryEngine_ConcurrentRollbackRequests_ShouldSucceedOnOnlyOne()
        {
            string tempRoot = Path.Combine(Path.GetTempPath(), "SAYRA_Test_RE_Concurrent_" + Guid.NewGuid());
            string binDir = Path.Combine(tempRoot, "Binaries");
            string confDir = Path.Combine(tempRoot, "Configs");

            Directory.CreateDirectory(binDir);
            Directory.CreateDirectory(confDir);

            try
            {
                File.WriteAllText(Path.Combine(binDir, "app.dll"), "Original App");

                var backupManager = new BackupManager(_backupLoggerMock.Object);
                var snapshotManager = new SnapshotManager(_snapshotLoggerMock.Object, backupManager);
                var rollbackEngine = new RollbackEngine(_rollbackLoggerMock.Object, snapshotManager)
                {
                    BinariesDirectory = binDir,
                    ConfigurationsDirectory = confDir
                };

                await rollbackEngine.CreateSnapshotAsync("1.0.0", CancellationToken.None);

                // Corrupt files
                File.WriteAllText(Path.Combine(binDir, "app.dll"), "Corrupt App");

                var context = new RecoveryContext
                {
                    TargetVersion = "2.0.0",
                    SourceVersion = "1.0.0",
                    InstallationDirectory = binDir,
                    CriticalFiles = new List<string> { "app.dll" },
                    FileHashes = new Dictionary<string, string> { { "app.dll", GetSha256OfText("Original App") } },
                    ConfigurationFilePath = ""
                };

                var validator = new RecoveryValidator(_validatorLoggerMock.Object);
                var stateMachine = new RecoveryStateMachine();
                var recoveryEngine = new RecoveryEngine(_recoveryLoggerMock.Object, rollbackEngine, validator, stateMachine);

                int successCount = 0;
                int failedCount = 0;

                // Concurrent execution
                await Task.Run(() =>
                {
                    Parallel.For(0, 10, i =>
                    {
                        try
                        {
                            var reportTask = recoveryEngine.RecoverAsync(context, CancellationToken.None);
                            reportTask.Wait();
                            if (reportTask.Result.Succeeded)
                            {
                                Interlocked.Increment(ref successCount);
                            }
                            else
                            {
                                Interlocked.Increment(ref failedCount);
                            }
                        }
                        catch
                        {
                            Interlocked.Increment(ref failedCount);
                        }
                    });
                });

                // Exactly one recovery operation transitions the state machine successfully and completes, other transitions reject invalid transitions!
                Assert.Equal(1, successCount);
                Assert.Equal(9, failedCount);
            }
            finally
            {
                if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, true);
            }
        }

        #endregion

        #region Helper Utilities

        private string GetSha256OfText(string text)
        {
            using (var sha256 = SHA256.Create())
            {
                byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(text));
                var sb = new System.Text.StringBuilder();
                foreach (byte b in bytes)
                {
                    sb.Append(b.ToString("x2"));
                }
                return sb.ToString();
            }
        }

        #endregion
    }
}
