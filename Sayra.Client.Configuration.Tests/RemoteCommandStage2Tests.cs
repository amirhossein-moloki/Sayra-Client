using System;
using System.Collections.Generic;
using System.Data.Common;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Sayra.Client.Shared.Interfaces;
using Sayra.Client.Shared.Models;
using SayraClient.RemoteOperations.Services;
using SayraClient.Services;
using Xunit;

namespace Sayra.Client.Configuration.Tests
{
    [Collection("Stage2Tests")]
    public class RemoteCommandStage2Tests : IDisposable
    {
        private readonly string _testDbDir;
        private readonly string _testDbPath;
        private readonly Mock<ILogger<LocalDatabaseService>> _dbLoggerMock;
        private readonly Mock<ILogger<DatabaseMigrationService>> _migrationLoggerMock;
        private readonly Mock<ILogger<RemoteCommandRepository>> _repoLoggerMock;
        private readonly Mock<ILogger<DeadLetterQueue>> _dlqLoggerMock;
        private readonly Mock<ILogger<OfflineCommandQueue>> _queueLoggerMock;
        private readonly Mock<ILogger<AuditService>> _auditLoggerMock;
        private readonly Mock<ILogger<CommandRetryWorker>> _retryLoggerMock;
        private readonly Mock<ILogger<RemoteCommandEngine>> _engineLoggerMock;
        private readonly Mock<IServiceHealthMonitor> _healthMonitorMock;
        private readonly Mock<IRemoteCommandDispatcher> _dispatcherMock;
        private readonly Mock<ICommandResultReporter> _reporterMock;
        private readonly IConfiguration _configuration;

        public RemoteCommandStage2Tests()
        {
            // Use a unique directory per test run to ensure absolute database isolation!
            _testDbDir = Path.Combine(AppContext.BaseDirectory, "Stage2TestData", Guid.NewGuid().ToString());
            if (Directory.Exists(_testDbDir))
            {
                Directory.Delete(_testDbDir, true);
            }
            Directory.CreateDirectory(_testDbDir);

            _testDbPath = Path.Combine(_testDbDir, "remote_commands.db");

            _dbLoggerMock = new Mock<ILogger<LocalDatabaseService>>();
            _migrationLoggerMock = new Mock<ILogger<DatabaseMigrationService>>();
            _repoLoggerMock = new Mock<ILogger<RemoteCommandRepository>>();
            _dlqLoggerMock = new Mock<ILogger<DeadLetterQueue>>();
            _queueLoggerMock = new Mock<ILogger<OfflineCommandQueue>>();
            _auditLoggerMock = new Mock<ILogger<AuditService>>();
            _retryLoggerMock = new Mock<ILogger<CommandRetryWorker>>();
            _engineLoggerMock = new Mock<ILogger<RemoteCommandEngine>>();
            _healthMonitorMock = new Mock<IServiceHealthMonitor>();
            _dispatcherMock = new Mock<IRemoteCommandDispatcher>();
            _reporterMock = new Mock<ICommandResultReporter>();

            var inMemoryConfig = new Dictionary<string, string?>
            {
                { "RemoteCommands:MaxRetryCount", "4" }
            };
            _configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(inMemoryConfig)
                .Build();

            // Set the environment variable so LocalDatabaseService picks up our test database path!
            Environment.SetEnvironmentVariable("SAYRA_TEST_DB_PATH", _testDbPath);
        }

        public void Dispose()
        {
            SqliteConnection.ClearAllPools();
            GC.Collect();
            GC.WaitForPendingFinalizers();

            try
            {
                if (Directory.Exists(_testDbDir))
                {
                    Directory.Delete(_testDbDir, true);
                }
            }
            catch { }

            Environment.SetEnvironmentVariable("SAYRA_TEST_DB_PATH", null);
        }

        private LocalDatabaseService CreateDbService()
        {
            var migrationService = new DatabaseMigrationService(_migrationLoggerMock.Object);
            return new LocalDatabaseService(_dbLoggerMock.Object, migrationService, null);
        }

        #region Database Tests

        [Fact]
        public async Task Database_Creation_And_Migration_Executes_Successfully()
        {
            using var dbService = CreateDbService();
            await dbService.InitializeDatabaseAsync();

            Assert.True(File.Exists(_testDbPath));

            // Verify integrity
            bool integrity = await dbService.VerifyIntegrityAsync();
            Assert.True(integrity);

            // Verify tables can be queried
            using var connection = dbService.CreateConnection();
            await connection.OpenAsync();

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM SchemaVersion;";
            var versionCount = Convert.ToInt32(await cmd.ExecuteScalarAsync());
            Assert.True(versionCount >= 1);
        }

        [Fact]
        public async Task Database_Encryption_Is_Enforced_And_Active()
        {
            using var dbService = CreateDbService();
            await dbService.InitializeDatabaseAsync();

            // Try opening SQLite file directly without encryption key/password
            var rawConnectionString = $"Data Source={_testDbPath};";
            using var rawConn = new SqliteConnection(rawConnectionString);

            // Opening is fine, but executing a query should throw since it's encrypted with SQLCipher
            await rawConn.OpenAsync();
            using var cmd = rawConn.CreateCommand();
            cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table';";

            var ex = await Assert.ThrowsAnyAsync<SqliteException>(() => cmd.ExecuteNonQueryAsync());
            Assert.True(ex.SqliteErrorCode == 26 || ex.Message.Contains("encrypted") || ex.Message.Contains("file is not a database") || ex.Message.Contains("not authorized"));
        }

        #endregion

        #region Repository Tests

        [Fact]
        public async Task Repository_Saves_Gets_And_Queries_History_Successfully()
        {
            using var dbService = CreateDbService();
            await dbService.InitializeDatabaseAsync();

            var repo = new RemoteCommandRepository(dbService, _repoLoggerMock.Object);

            var cmdId = Guid.NewGuid().ToString();
            var command = new RemoteCommandHistory
            {
                CommandId = cmdId,
                Action = "LOCK_PC",
                TargetPcId = "PC-001",
                SenderAdminId = "ADMIN-99",
                PayloadJson = "{}",
                Status = "PENDING",
                ReceivedAt = DateTime.UtcNow.ToString("O"),
                Signature = "rsa-sig-bytes",
                RetryCount = 0
            };

            await repo.SaveCommandAsync(command);

            var retrieved = await repo.GetCommandAsync(cmdId);
            Assert.NotNull(retrieved);
            Assert.Equal("LOCK_PC", retrieved.Action);
            Assert.Equal("PENDING", retrieved.Status);
            Assert.Equal("PC-001", retrieved.TargetPcId);

            // Update status and duration
            await repo.UpdateStatusAsync(cmdId, "EXECUTING");
            var executing = await repo.GetCommandAsync(cmdId);
            Assert.Equal("EXECUTING", executing!.Status);
            Assert.NotNull(executing.StartedAt);

            // Delay simulated and complete
            await Task.Delay(50);
            await repo.UpdateStatusAsync(cmdId, "COMPLETED");
            var completed = await repo.GetCommandAsync(cmdId);
            Assert.Equal("COMPLETED", completed!.Status);
            Assert.NotNull(completed.CompletedAt);
            Assert.True(completed.ExecutionDurationMs >= 0);

            // Get pending
            var pendingList = await repo.GetPendingCommandsAsync();
            Assert.Empty(pendingList);
        }

        #endregion

        #region Queue Restore & Retry Tests

        [Fact]
        public async Task Offline_Command_Queue_Restores_Pending_And_Interrupted_Commands()
        {
            using var dbService = CreateDbService();
            await dbService.InitializeDatabaseAsync();

            var repo = new RemoteCommandRepository(dbService, _repoLoggerMock.Object);

            // 1. Pending command
            var cmdId1 = Guid.NewGuid().ToString();
            var cmd1 = new RemoteCommandHistory
            {
                CommandId = cmdId1,
                Action = "UNLOCK_PC",
                TargetPcId = "PC-001",
                SenderAdminId = "ADMIN-99",
                PayloadJson = "{}",
                Status = "PENDING",
                ReceivedAt = DateTime.UtcNow.AddSeconds(-1).ToString("O"),
                Signature = "sig-1"
            };
            await repo.SaveCommandAsync(cmd1);

            // 2. Interrupted (Executing) command
            var cmdId2 = Guid.NewGuid().ToString();
            var cmd2 = new RemoteCommandHistory
            {
                CommandId = cmdId2,
                Action = "SHUTDOWN",
                TargetPcId = "PC-001",
                SenderAdminId = "ADMIN-99",
                PayloadJson = "{}",
                Status = "EXECUTING",
                ReceivedAt = DateTime.UtcNow.ToString("O"),
                Signature = "sig-2"
            };
            await repo.SaveCommandAsync(cmd2);

            // Setup DI container with mocks to emulate Engine
            var services = new ServiceCollection();
            var engineMock = new Mock<IRemoteCommandEngine>();
            var queuedCommands = new List<RemoteCommand>();

            engineMock.Setup(e => e.QueueCommandAsync(It.IsAny<RemoteCommand>()))
                .Callback<RemoteCommand>(rc => queuedCommands.Add(rc))
                .Returns(Task.CompletedTask);

            services.AddSingleton(engineMock.Object);
            var serviceProvider = services.BuildServiceProvider();

            var offlineQueue = new OfflineCommandQueue(repo, serviceProvider, _queueLoggerMock.Object);
            await offlineQueue.RestoreAndResumeQueueAsync();

            Assert.Equal(2, queuedCommands.Count);
            // Verify ordering (cmd1 received earlier, should be queued first)
            Assert.Equal(cmdId1, queuedCommands[0].CommandId.ToString());
            Assert.Equal(cmdId2, queuedCommands[1].CommandId.ToString());

            // Check that the interrupted command's status was reset to PENDING in history
            var updatedCmd2 = await repo.GetCommandAsync(cmdId2);
            Assert.Equal("PENDING", updatedCmd2!.Status);
        }

        [Fact]
        public async Task Retry_Worker_Triggers_Backoff_Retries_And_Moves_To_DLQ_On_Max_Failures()
        {
            using var dbService = CreateDbService();
            await dbService.InitializeDatabaseAsync();

            var repo = new RemoteCommandRepository(dbService, _repoLoggerMock.Object);
            var dlq = new DeadLetterQueue(dbService, repo, _dlqLoggerMock.Object);

            var cmdId = Guid.NewGuid().ToString();
            var failedCommand = new RemoteCommandHistory
            {
                CommandId = cmdId,
                Action = "RESTART",
                TargetPcId = "PC-001",
                SenderAdminId = "ADMIN-99",
                PayloadJson = "{}",
                Status = "FAILED",
                ReceivedAt = DateTime.UtcNow.AddMinutes(-1).ToString("O"),
                CompletedAt = DateTime.UtcNow.AddSeconds(-6).ToString("O"), // 6s ago, retry 1 delay is 5s
                Signature = "sig",
                RetryCount = 0
            };
            await repo.SaveCommandAsync(failedCommand);

            var services = new ServiceCollection();
            var engineMock = new Mock<IRemoteCommandEngine>();
            engineMock.Setup(e => e.QueueCommandAsync(It.IsAny<RemoteCommand>())).Returns(Task.CompletedTask);
            services.AddSingleton(engineMock.Object);
            var serviceProvider = services.BuildServiceProvider();

            var retryWorker = new CommandRetryWorker(
                _retryLoggerMock.Object,
                _healthMonitorMock.Object,
                repo,
                dlq,
                serviceProvider,
                _configuration
            );

            // Execute retry logic manually
            using var cts = new CancellationTokenSource();
            var task = Task.Run(() => retryWorker.RunSupervisedAsync(cts.Token));
            await Task.Delay(200);
            cts.Cancel();
            try { await task; } catch { }

            // Verify that command was retried (status reset to PENDING, retry count incremented to 1)
            var retried = await repo.GetCommandAsync(cmdId);
            Assert.Equal("PENDING", retried!.Status);
            Assert.Equal(1, retried.RetryCount);

            // Set retry count to max (4) and make it fail again
            retried.RetryCount = 4;
            retried.Status = "FAILED";
            // Important: Set CompletedAt to 31 minutes ago to satisfy the 30-minute attempt 5 backoff window!
            retried.CompletedAt = DateTime.UtcNow.AddMinutes(-31).ToString("O");
            await repo.SaveCommandAsync(retried);

            // Run retry worker again to trigger DLQ movement
            using var cts2 = new CancellationTokenSource();
            var task2 = Task.Run(() => retryWorker.RunSupervisedAsync(cts2.Token));
            await Task.Delay(200);
            cts2.Cancel();
            try { await task2; } catch { }

            // Verify that command was moved to DLQ (status updated in history, record created in DLQ)
            var finalHistory = await repo.GetCommandAsync(cmdId);
            Assert.Equal("FAILED_DLQ", finalHistory!.Status);

            var dlqCmd = await dlq.GetDeadLetterCommandAsync(cmdId);
            Assert.NotNull(dlqCmd);
            Assert.Equal("RESTART", dlqCmd.OriginalAction);
            Assert.Equal(4, dlqCmd.RetryCount);
        }

        #endregion

        #region Audit Hashing & Tamper Detection Tests

        [Fact]
        public async Task Audit_Service_Generates_Valid_Hash_Chains_And_Detects_Database_Tampering()
        {
            using var dbService = CreateDbService();
            await dbService.InitializeDatabaseAsync();

            var auditService = new AuditService(dbService, _auditLoggerMock.Object);

            var cmdId = Guid.NewGuid().ToString();
            await auditService.RecordCommandReceivedAsync(cmdId, "LOCK_PC", "corr-1");
            await auditService.RecordSecurityValidationResultAsync(cmdId, true, "Valid", "corr-1");
            await auditService.RecordExecutionStartedAsync(cmdId, "LOCK_PC", "corr-1");
            await auditService.RecordExecutionCompletedAsync(cmdId, "LOCK_PC", "corr-1");

            var trail = await auditService.GetAuditTrailAsync();
            Assert.Equal(4, trail.Count);

            // Verify chain integrity
            bool integrityBefore = await auditService.VerifyAuditChainIntegrityAsync();
            Assert.True(integrityBefore);

            // Corrupt database record directly using raw SQL
            using (var connection = dbService.CreateConnection())
            {
                await connection.OpenAsync();
                using var cmd = connection.CreateCommand();
                cmd.CommandText = "UPDATE AuditEntry SET Details = 'TAMPERED_DETAILS' WHERE EventType = 'SECURITY_VALIDATION_PASSED';";
                await cmd.ExecuteNonQueryAsync();
            }

            // Verify chain integrity after tampering
            bool integrityAfter = await auditService.VerifyAuditChainIntegrityAsync();
            Assert.False(integrityAfter);
        }

        #endregion

        #region Extreme Failure Tests

        [Fact]
        public async Task LocalDatabaseService_Handles_Corrupted_Database_By_Safely_Recreating_And_Recovering()
        {
            using var dbService = CreateDbService();
            await dbService.InitializeDatabaseAsync();

            // Populate some data first
            var repo = new RemoteCommandRepository(dbService, _repoLoggerMock.Object);
            await repo.SaveCommandAsync(new RemoteCommandHistory
            {
                CommandId = Guid.NewGuid().ToString(),
                Action = "SHUTDOWN",
                Status = "PENDING",
                ReceivedAt = DateTime.UtcNow.ToString("O"),
                Signature = "sig"
            });

            // Close connection, clear pools, and corrupt file manually
            await dbService.CloseSafelyAsync();
            SqliteConnection.ClearAllPools();
            GC.Collect();
            GC.WaitForPendingFinalizers();

            // Corrupt file by writing random garbage bytes
            byte[] garbage = new byte[2048];
            RandomNumberGenerator.Fill(garbage);
            File.WriteAllBytes(_testDbPath, garbage);

            // Re-initialize. The DB service should catch the exception and recreate a healthy one.
            using var freshDbService = CreateDbService();
            await freshDbService.InitializeDatabaseAsync();

            // Verify that the database is usable again
            bool integrity = await freshDbService.VerifyIntegrityAsync();
            Assert.True(integrity);

            // Verify that a backup file or recreation is done
            Assert.True(File.Exists(_testDbPath));
        }

        [Fact]
        public async Task Database_Locked_Throws_And_Fails_Gracefully()
        {
            using var dbService = CreateDbService();
            await dbService.InitializeDatabaseAsync();

            using var connection1 = dbService.CreateConnection();
            await connection1.OpenAsync();

            using var transaction1 = await connection1.BeginTransactionAsync();
            using var cmd1 = connection1.CreateCommand();
            cmd1.Transaction = transaction1;
            cmd1.CommandText = "INSERT INTO SchemaVersion (Version, AppliedAt) VALUES (99, '2024');";
            await cmd1.ExecuteNonQueryAsync();

            // Now, open another connection and try to write. Since transaction1 is not committed,
            // the DB is locked and second write should eventually timeout / fail gracefully.
            using var connection2 = dbService.CreateConnection();
            await connection2.OpenAsync();

            using var cmd2 = connection2.CreateCommand();
            cmd2.CommandText = "INSERT INTO SchemaVersion (Version, AppliedAt) VALUES (100, '2024');";

            // SQLite will block and eventually throw locked/busy exception
            var ex = await Assert.ThrowsAsync<SqliteException>(() => cmd2.ExecuteNonQueryAsync());
            Assert.True(ex.SqliteErrorCode == 5 || ex.Message.Contains("locked") || ex.Message.Contains("busy"));

            await transaction1.RollbackAsync();
        }

        #endregion
    }
}
