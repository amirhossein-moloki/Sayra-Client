using System;
using System.Collections.Generic;
using System.IO;
using System.Security;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Sayra.Client.Shared.Interfaces;
using Sayra.Client.Shared.Interfaces.Security;
using Sayra.Client.Shared.Models;
using SayraClient.RemoteOperations.Services;
using Xunit;

namespace Sayra.Client.Configuration.Tests
{
    [Collection("Stage5Tests")]
    public class FleetManagementTests : IDisposable
    {
        private readonly string _testDbDir;
        private readonly string _testDbPath;
        private readonly Mock<ILogger<LocalDatabaseService>> _dbLoggerMock;
        private readonly Mock<ILogger<DatabaseMigrationService>> _migrationLoggerMock;
        private readonly Mock<ILogger<GroupRepository>> _repoLoggerMock;
        private readonly Mock<ILogger<FleetManager>> _fleetLoggerMock;
        private readonly Mock<ILogger<BulkOperationService>> _bulkLoggerMock;
        private readonly Mock<ILogger<AlertEngine>> _alertLoggerMock;
        private readonly Mock<ILogger<AuditService>> _auditLoggerMock;
        private readonly Mock<ILogger<OperationCoordinator>> _coordLoggerMock;
        private readonly Mock<ILogger<EnterpriseOperationService>> _enterpriseLoggerMock;

        private readonly Mock<ISignatureVerifier> _sigVerifierMock;

        public FleetManagementTests()
        {
            _testDbDir = Path.Combine(AppContext.BaseDirectory, "Stage5TestData", Guid.NewGuid().ToString());
            if (Directory.Exists(_testDbDir))
            {
                Directory.Delete(_testDbDir, true);
            }
            Directory.CreateDirectory(_testDbDir);

            _testDbPath = Path.Combine(_testDbDir, "remote_commands.db");

            _dbLoggerMock = new Mock<ILogger<LocalDatabaseService>>();
            _migrationLoggerMock = new Mock<ILogger<DatabaseMigrationService>>();
            _repoLoggerMock = new Mock<ILogger<GroupRepository>>();
            _fleetLoggerMock = new Mock<ILogger<FleetManager>>();
            _bulkLoggerMock = new Mock<ILogger<BulkOperationService>>();
            _alertLoggerMock = new Mock<ILogger<AlertEngine>>();
            _auditLoggerMock = new Mock<ILogger<AuditService>>();
            _coordLoggerMock = new Mock<ILogger<OperationCoordinator>>();
            _enterpriseLoggerMock = new Mock<ILogger<EnterpriseOperationService>>();

            _sigVerifierMock = new Mock<ISignatureVerifier>();
            _sigVerifierMock.Setup(s => s.VerifySignature(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                            .Returns(true);

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

        #region Database & Migration Tests

        [Fact]
        public async Task Database_Migration_3_Executes_Successfully()
        {
            using var dbService = CreateDbService();
            await dbService.InitializeDatabaseAsync();

            Assert.True(File.Exists(_testDbPath));

            using var connection = dbService.CreateConnection();
            await connection.OpenAsync();

            // Verify tables can be queried
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name IN ('Workstations', 'MachineGroups', 'MachineAssignments', 'BulkOperations', 'BulkOperationResults', 'FleetAlerts', 'DynamicCollections', 'CollectionMembership');";
            var tableCount = Convert.ToInt32(await cmd.ExecuteScalarAsync());
            Assert.Equal(8, tableCount);
        }

        #endregion

        #region Workstation & Fleet Management Tests

        [Fact]
        public async Task Workstation_Registration_And_Metadata_Updates()
        {
            using var dbService = CreateDbService();
            await dbService.InitializeDatabaseAsync();

            var repo = new GroupRepository(dbService, _repoLoggerMock.Object);
            var fleet = new FleetManager(dbService, repo, _fleetLoggerMock.Object);

            var ws = new Workstation
            {
                WorkstationId = "WS-01",
                Name = "GamerPC-01",
                IpAddress = "192.168.1.10",
                MacAddress = "00:11:22:33:44:55",
                Status = "Online",
                LastSeen = DateTime.UtcNow.ToString("O"),
                Version = "1.0.0",
                Gpu = "RTX3080",
                RamGb = 16,
                WindowsVersion = "10 Pro",
                PolicyVersion = "v1",
                HealthState = "Healthy"
            };

            await fleet.RegisterWorkstationAsync(ws);

            var retrieved = await fleet.GetWorkstationAsync("WS-01");
            Assert.NotNull(retrieved);
            Assert.Equal("GamerPC-01", retrieved.Name);
            Assert.Equal("RTX3080", retrieved.Gpu);

            // Update Metadata
            await fleet.UpdateMetadataAsync("WS-01", "192.168.1.11", "00:11:22:33:44:55", "1.1.0", "RTX4090", 32, "11 Pro", "v2");

            var updated = await fleet.GetWorkstationAsync("WS-01");
            Assert.NotNull(updated);
            Assert.Equal("RTX4090", updated.Gpu);
            Assert.Equal(32, updated.RamGb);
            Assert.Equal("11 Pro", updated.WindowsVersion);
            Assert.Equal("v2", updated.PolicyVersion);
        }

        #endregion

        #region Group & Assignment Tests

        [Fact]
        public async Task Group_Creation_And_Assignment()
        {
            using var dbService = CreateDbService();
            await dbService.InitializeDatabaseAsync();

            var repo = new GroupRepository(dbService, _repoLoggerMock.Object);
            var fleet = new FleetManager(dbService, repo, _fleetLoggerMock.Object);

            var grp = new MachineGroup
            {
                GroupId = "GRP-VIP",
                Name = "VIP Room",
                Description = "High end systems",
                GroupType = "Static"
            };

            await repo.CreateGroupAsync(grp);

            var retrievedGrp = await repo.GetGroupAsync("GRP-VIP");
            Assert.NotNull(retrievedGrp);
            Assert.Equal("VIP Room", retrievedGrp.Name);

            // Register machine and assign
            var ws = new Workstation { WorkstationId = "WS-01", Name = "VIP-01" };
            await fleet.RegisterWorkstationAsync(ws);

            await repo.AssignMachineAsync("WS-01", "GRP-VIP");

            var machines = await repo.GetMachinesAsync("GRP-VIP");
            Assert.Single(machines);
            Assert.Equal("WS-01", machines[0].WorkstationId);

            // Remove assignment
            await repo.RemoveMachineAsync("WS-01", "GRP-VIP");
            var machinesAfter = await repo.GetMachinesAsync("GRP-VIP");
            Assert.Empty(machinesAfter);
        }

        #endregion

        #region Dynamic Collections Tests

        [Fact]
        public async Task Dynamic_Collection_Rule_Evaluation()
        {
            using var dbService = CreateDbService();
            await dbService.InitializeDatabaseAsync();

            var repo = new GroupRepository(dbService, _repoLoggerMock.Object);
            var fleet = new FleetManager(dbService, repo, _fleetLoggerMock.Object);

            // Create Dynamic Collection
            var col = new DynamicCollection
            {
                CollectionId = "COL-RTX4090",
                Name = "RTX 4090 Systems",
                RuleExpression = "GPU == RTX4090"
            };
            await fleet.CreateDynamicCollectionAsync(col);

            // Workstation 1: RTX 4090
            var ws1 = new Workstation
            {
                WorkstationId = "WS-01",
                Name = "PC-01",
                Gpu = "NVIDIA GeForce RTX4090",
                RamGb = 32
            };
            await fleet.RegisterWorkstationAsync(ws1);

            // Workstation 2: RTX 3080
            var ws2 = new Workstation
            {
                WorkstationId = "WS-02",
                Name = "PC-02",
                Gpu = "NVIDIA GeForce RTX3080",
                RamGb = 16
            };
            await fleet.RegisterWorkstationAsync(ws2);

            var members = await fleet.GetCollectionMembersAsync("COL-RTX4090");
            Assert.Single(members);
            Assert.Equal("WS-01", members[0].WorkstationId);

            // Update Workstation 2 to RTX 4090 -> should auto update collection membership!
            await fleet.UpdateMetadataAsync("WS-02", "192.168.1.15", "00:aa", "1.0", "NVIDIA GeForce RTX4090", 32, "10", "v1");

            var membersAfter = await fleet.GetCollectionMembersAsync("COL-RTX4090");
            Assert.Equal(2, membersAfter.Count);
        }

        #endregion

        #region Bulk Operations Tests

        [Fact]
        public async Task Bulk_Operation_Execution_And_Tracking()
        {
            using var dbService = CreateDbService();
            await dbService.InitializeDatabaseAsync();

            var repo = new GroupRepository(dbService, _repoLoggerMock.Object);
            var fleet = new FleetManager(dbService, repo, _fleetLoggerMock.Object);
            var audit = new AuditService(dbService, _auditLoggerMock.Object);
            var bulkService = new BulkOperationService(dbService, fleet, repo, _sigVerifierMock.Object, audit, _bulkLoggerMock.Object);

            var ws1 = new Workstation { WorkstationId = "WS-01", Name = "PC-01", Status = "Online" };
            var ws2 = new Workstation { WorkstationId = "WS-02", Name = "PC-02", Status = "Online" };
            await fleet.RegisterWorkstationAsync(ws1);
            await fleet.RegisterWorkstationAsync(ws2);

            // Start bulk operation
            string opId = await bulkService.ExecuteBulkOperationAsync(
                action: "RESTART",
                targetGroupIds: null,
                targetCollectionId: null,
                targetEntireFleet: true,
                adminId: "ADMIN-01",
                signature: "VALID_TEST_SIGNATURE"
            );

            Assert.NotEmpty(opId);

            // Let background task complete (simulated fast in-memory delays)
            await Task.Delay(250);

            var op = await bulkService.GetBulkOperationAsync(opId);
            Assert.NotNull(op);
            Assert.Equal("Succeeded", op.Status);
            Assert.Equal(2, op.SucceededCount);

            var results = await bulkService.GetBulkOperationResultsAsync(opId);
            Assert.Equal(2, results.Count);
            Assert.True(results[0].Succeeded);
            Assert.True(results[1].Succeeded);
        }

        [Fact]
        public async Task Bulk_Operation_Cancellation()
        {
            using var dbService = CreateDbService();
            await dbService.InitializeDatabaseAsync();

            var repo = new GroupRepository(dbService, _repoLoggerMock.Object);
            var fleet = new FleetManager(dbService, repo, _fleetLoggerMock.Object);
            var audit = new AuditService(dbService, _auditLoggerMock.Object);
            var bulkService = new BulkOperationService(dbService, fleet, repo, _sigVerifierMock.Object, audit, _bulkLoggerMock.Object);

            var ws = new Workstation { WorkstationId = "WS-01", Name = "PC-01", Status = "Online" };
            await fleet.RegisterWorkstationAsync(ws);

            string opId = await bulkService.ExecuteBulkOperationAsync(
                action: "SHUTDOWN",
                targetGroupIds: null,
                targetCollectionId: null,
                targetEntireFleet: true,
                adminId: "ADMIN-01",
                signature: "VALID_TEST_SIGNATURE"
            );

            // Cancel immediately
            await bulkService.CancelBulkOperationAsync(opId);

            var op = await bulkService.GetBulkOperationAsync(opId);
            Assert.NotNull(op);
            Assert.Equal("Cancelled", op.Status);
        }

        #endregion

        #region Alert Management Tests

        [Fact]
        public async Task Alert_Generation_And_Suppression_And_Escalation()
        {
            using var dbService = CreateDbService();
            await dbService.InitializeDatabaseAsync();

            var audit = new AuditService(dbService, _auditLoggerMock.Object);
            var alertEngine = new AlertEngine(dbService, audit, _alertLoggerMock.Object);

            // Process GPU Temp > 90 (violates rule)
            await alertEngine.ProcessMetricAsync("WS-01", "GPU_TEMP", 95.5);

            var activeAlerts = await alertEngine.GetActiveAlertsAsync();
            Assert.Single(activeAlerts);
            Assert.Equal("WS-01", activeAlerts[0].WorkstationId);
            Assert.Equal("GPU_TEMP", activeAlerts[0].AlertType);
            Assert.Equal("Warning", activeAlerts[0].Severity); // Initial severity is Warning

            // Suppress duplicate alert (metrics processed again while active)
            await alertEngine.ProcessMetricAsync("WS-01", "GPU_TEMP", 96.0);
            var activeAlertsAfter = await alertEngine.GetActiveAlertsAsync();
            Assert.Single(activeAlertsAfter); // Still just 1 active alert!

            // Force evaluate escalation by modifying CreatedAt timestamp to 20 minutes ago
            using (var connection = dbService.CreateConnection())
            {
                await connection.OpenAsync();
                using var cmd = connection.CreateCommand();
                cmd.CommandText = "UPDATE FleetAlerts SET CreatedAt = $time;";
                cmd.Parameters.Add(new SqliteParameter("$time", DateTime.UtcNow.AddMinutes(-20).ToString("O")));
                await cmd.ExecuteNonQueryAsync();
            }

            // Trigger re-evaluation -> should escalate to Critical
            await alertEngine.ProcessMetricAsync("WS-01", "GPU_TEMP", 95.0);

            var activeAlertsEscalated = await alertEngine.GetActiveAlertsAsync();
            Assert.Single(activeAlertsEscalated);
            Assert.Equal("Critical", activeAlertsEscalated[0].Severity);
            Assert.Equal(1, activeAlertsEscalated[0].Escalated);

            // Auto-resolve when value goes back to normal
            await alertEngine.ProcessMetricAsync("WS-01", "GPU_TEMP", 80.0);
            var activeAlertsResolved = await alertEngine.GetActiveAlertsAsync();
            Assert.Empty(activeAlertsResolved);
        }

        #endregion

        #region Concurrency & Security Tests

        [Fact]
        public async Task Operation_Coordinator_Concurrency()
        {
            var coordinator = new OperationCoordinator(_coordLoggerMock.Object);

            // Acquire lock on remote command
            using var lock1 = await coordinator.TryAcquireLockAsync("REMOTE_COMMAND");
            Assert.NotNull(lock1);

            // Try to acquire conflicting lock on bulk operation -> should fail!
            using var lock2 = await coordinator.TryAcquireLockAsync("BULK_OPERATION");
            Assert.Null(lock2);

            // Try duplicate execute same remote command type -> should fail!
            using var lock3 = await coordinator.TryAcquireLockAsync("REMOTE_COMMAND");
            Assert.Null(lock3);

            // Release lock1
            lock1.Dispose();

            // Try again -> should now succeed!
            using var lock4 = await coordinator.TryAcquireLockAsync("BULK_OPERATION");
            Assert.NotNull(lock4);
        }

        [Fact]
        public async Task Bulk_Operation_Signature_Rejection()
        {
            using var dbService = CreateDbService();
            await dbService.InitializeDatabaseAsync();

            var repo = new GroupRepository(dbService, _repoLoggerMock.Object);
            var fleet = new FleetManager(dbService, repo, _fleetLoggerMock.Object);
            var audit = new AuditService(dbService, _auditLoggerMock.Object);

            var badSigVerifier = new Mock<ISignatureVerifier>();
            badSigVerifier.Setup(s => s.VerifySignature(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                          .Returns(false);

            var bulkService = new BulkOperationService(dbService, fleet, repo, badSigVerifier.Object, audit, _bulkLoggerMock.Object);

            // Call with invalid signature -> should throw SecurityException
            await Assert.ThrowsAsync<SecurityException>(() => bulkService.ExecuteBulkOperationAsync(
                action: "LOCK",
                targetGroupIds: null,
                targetCollectionId: null,
                targetEntireFleet: true,
                adminId: "ADMIN-01",
                signature: "BAD_SIGNATURE"
            ));
        }

        #endregion
    }
}
