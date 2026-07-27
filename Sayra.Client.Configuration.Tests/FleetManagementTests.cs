using System;
using System.Collections.Generic;
using System.IO;
using System.Security;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Sayra.Client.Shared.Interfaces;
using Sayra.Client.Shared.Models;
using SayraClient.RemoteOperations.Services;
using SayraClient.RemoteOperations.Services.Fleet;
using Xunit;

namespace Sayra.Client.Configuration.Tests
{
    [Collection("Stage5Tests")]
    public class FleetManagementTests : IDisposable
    {
        private readonly string _testDbDir;
        private readonly string _testDbPath;
        private readonly Mock<ILogger<LocalDatabaseService>> _dbLogger;
        private readonly Mock<ILogger<DatabaseMigrationService>> _migrationLogger;
        private readonly Mock<ILogger<GroupRepository>> _repoLogger;
        private readonly Mock<ILogger<FleetManager>> _fleetLogger;
        private readonly Mock<ILogger<DynamicCollectionEngine>> _collLogger;
        private readonly Mock<ILogger<BulkOperationService>> _bulkLogger;
        private readonly Mock<ILogger<AlertEngine>> _alertLogger;
        private readonly Mock<ILogger<EnterpriseOperationService>> _entLogger;
        private readonly Mock<ILogger<OperationCoordinator>> _coordLogger;
        private readonly Mock<IAuditService> _auditMock;

        private readonly LocalDatabaseService _dbService;
        private readonly GroupRepository _groupRepo;
        private readonly DynamicCollectionEngine _collEngine;
        private readonly FleetManager _fleetManager;
        private readonly BulkOperationService _bulkService;
        private readonly AlertEngine _alertEngine;
        private readonly EnterpriseOperationService _enterpriseService;
        private readonly OperationCoordinator _coordinator;

        public FleetManagementTests()
        {
            _testDbDir = Path.Combine(AppContext.BaseDirectory, "Stage5TestData", Guid.NewGuid().ToString());
            Directory.CreateDirectory(_testDbDir);
            _testDbPath = Path.Combine(_testDbDir, "fleet_commands.db");

            Environment.SetEnvironmentVariable("SAYRA_TEST_DB_PATH", _testDbPath);

            _dbLogger = new Mock<ILogger<LocalDatabaseService>>();
            _migrationLogger = new Mock<ILogger<DatabaseMigrationService>>();
            _repoLogger = new Mock<ILogger<GroupRepository>>();
            _fleetLogger = new Mock<ILogger<FleetManager>>();
            _collLogger = new Mock<ILogger<DynamicCollectionEngine>>();
            _bulkLogger = new Mock<ILogger<BulkOperationService>>();
            _alertLogger = new Mock<ILogger<AlertEngine>>();
            _entLogger = new Mock<ILogger<EnterpriseOperationService>>();
            _coordLogger = new Mock<ILogger<OperationCoordinator>>();
            _auditMock = new Mock<IAuditService>();

            var migrationService = new DatabaseMigrationService(_migrationLogger.Object);
            _dbService = new LocalDatabaseService(_dbLogger.Object, migrationService, null);

            _groupRepo = new GroupRepository(_dbService, _repoLogger.Object);
            _collEngine = new DynamicCollectionEngine(_dbService, _collLogger.Object);
            _fleetManager = new FleetManager(_dbService, _groupRepo, _fleetLogger.Object);
            _alertEngine = new AlertEngine(_dbService, _auditMock.Object, _alertLogger.Object);
            _enterpriseService = new EnterpriseOperationService(_dbService, _entLogger.Object);
            _coordinator = new OperationCoordinator(_coordLogger.Object);

            _bulkService = new BulkOperationService(
                _dbService,
                _groupRepo,
                _collEngine,
                _fleetManager,
                _auditMock.Object,
                _bulkLogger.Object
            );
        }

        public void Dispose()
        {
            _dbService.CloseSafelyAsync().Wait();
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
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

        [Fact]
        public async Task Database_Migration_Version3_Applied_Successfully()
        {
            await _dbService.InitializeDatabaseAsync();

            using var connection = _dbService.CreateConnection();
            await connection.OpenAsync();

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT MAX(Version) FROM SchemaVersion;";
            var version = Convert.ToInt32(await cmd.ExecuteScalarAsync());
            Assert.Equal(3, version);
        }

        [Fact]
        public async Task Workstation_Registration_And_Metadata_Management()
        {
            await _dbService.InitializeDatabaseAsync();

            var metadata = new Dictionary<string, string>
            {
                { "CpuUsage", "25.5" },
                { "GPU", "RTX4090" },
                { "RAM", "32GB" }
            };

            await _fleetManager.RegisterWorkstationAsync("PC-101", metadata);

            var status = await _fleetManager.QueryWorkstationStatusAsync("PC-101");
            Assert.Equal("Online", status);

            var caps = await _fleetManager.QueryWorkstationCapabilitiesAsync("PC-101");
            Assert.Equal("RTX4090", caps["GPU"]);

            metadata["CpuUsage"] = "60.0";
            await _fleetManager.UpdateWorkstationMetadataAsync("PC-101", metadata);

            caps = await _fleetManager.QueryWorkstationCapabilitiesAsync("PC-101");
            Assert.Equal("60.0", caps["CpuUsage"]);
        }

        [Fact]
        public async Task Group_Creation_And_Assignment_Persistence()
        {
            await _dbService.InitializeDatabaseAsync();

            var group = new MachineGroup
            {
                GroupId = "VIP-ZONE",
                Name = "VIP Hall",
                Description = "High-end specs section",
                IsDynamic = false
            };

            await _groupRepo.CreateGroupAsync(group);

            var retrieved = await _groupRepo.GetGroupAsync("VIP-ZONE");
            Assert.NotNull(retrieved);
            Assert.Equal("VIP Hall", retrieved.Name);

            await _groupRepo.AssignMachineAsync("PC-101", "VIP-ZONE");

            var machines = await _groupRepo.GetMachinesAsync("VIP-ZONE");
            Assert.Contains("PC-101", machines);

            await _groupRepo.RemoveMachineAsync("PC-101", "VIP-ZONE");
            machines = await _groupRepo.GetMachinesAsync("VIP-ZONE");
            Assert.DoesNotContain("PC-101", machines);
        }

        [Fact]
        public async Task Dynamic_Collection_Rule_Evaluation()
        {
            await _dbService.InitializeDatabaseAsync();

            var pc1Meta = new Dictionary<string, string> { { "GPU", "RTX4090" }, { "RAM", "32" } };
            var pc2Meta = new Dictionary<string, string> { { "GPU", "GTX1060" }, { "RAM", "16" } };

            await _fleetManager.RegisterWorkstationAsync("PC-1", pc1Meta);
            await _fleetManager.RegisterWorkstationAsync("PC-2", pc2Meta);

            var coll = new DynamicCollection
            {
                CollectionId = "RTX4090-FLEET",
                Name = "RTX 4090 Workstations",
                RuleJson = "{\"Metric\": \"GPU\", \"Operator\": \"==\", \"Value\": \"RTX4090\"}"
            };

            await _collEngine.CreateCollectionAsync(coll);

            var machines = await _collEngine.GetCollectionMachinesAsync("RTX4090-FLEET");
            Assert.Single(machines);
            Assert.Contains("PC-1", machines);
        }

        [Fact]
        public async Task Bulk_Operation_Execution_Retry_And_Cancellation()
        {
            await _dbService.InitializeDatabaseAsync();

            await _fleetManager.RegisterWorkstationAsync("PC-1", new Dictionary<string, string>());
            await _fleetManager.RegisterWorkstationAsync("PC-2-offline", new Dictionary<string, string>());

            var group = new MachineGroup { GroupId = "G-1", Name = "Group 1", Description = "", IsDynamic = false };
            await _groupRepo.CreateGroupAsync(group);
            await _groupRepo.AssignMachineAsync("PC-1", "G-1");
            await _groupRepo.AssignMachineAsync("PC-2-offline", "G-1");

            var op = await _bulkService.StartBulkOperationAsync("Restart", "Group", "G-1", "{}");

            Assert.Equal(BulkOperationStatus.Running, op.Status);

            await Task.Delay(250);

            var status = await _bulkService.GetBulkOperationStatusAsync(op.OperationId);
            Assert.NotNull(status);

            var results = await _bulkService.GetBulkOperationResultsAsync(op.OperationId);
            Assert.Equal(2, results.Count);

            await _bulkService.CancelBulkOperationAsync(op.OperationId);
            status = await _bulkService.GetBulkOperationStatusAsync(op.OperationId);
            Assert.Equal(BulkOperationStatus.Cancelled, status!.Status);
        }

        [Fact]
        public async Task Alert_Generation_Suppression_And_Escalation()
        {
            await _dbService.InitializeDatabaseAsync();

            var rule = new AlertRule
            {
                RuleId = "TEMP-RULE",
                MetricName = "GpuTemp",
                Operator = ">",
                Threshold = "85",
                Severity = "Warning",
                CooldownSeconds = 0, // Disable cooldown so escalation check is reached
                EscalationTimeoutSeconds = 0, // Escalate immediately on second check
                AutoResolve = true,
                EscalationPath = "Email"
            };

            await _alertEngine.ConfigureRuleAsync(rule);

            await _alertEngine.ProcessMetricAsync("PC-101", "GpuTemp", "90");

            var activeAlerts = await _alertEngine.GetActiveAlertsAsync();
            Assert.Single(activeAlerts);
            Assert.Equal("Warning", activeAlerts[0].Severity);

            // Trigger second check to escalate immediately
            await _alertEngine.ProcessMetricAsync("PC-101", "GpuTemp", "92");

            activeAlerts = await _alertEngine.GetActiveAlertsAsync();
            Assert.Single(activeAlerts);
            Assert.Equal(1, activeAlerts[0].EscalationLevel);
            Assert.Equal("Critical", activeAlerts[0].Severity);

            await _alertEngine.ProcessMetricAsync("PC-101", "GpuTemp", "70");
            activeAlerts = await _alertEngine.GetActiveAlertsAsync();
            Assert.Empty(activeAlerts);
        }

        [Fact]
        public async Task OperationCoordinator_Prevents_Duplicate_Or_Conflicting_Executions()
        {
            bool acquired1 = await _coordinator.AcquireLockAsync("PC-101", "Restart");
            bool acquired2 = await _coordinator.AcquireLockAsync("PC-101", "Shutdown");

            Assert.True(acquired1);
            Assert.False(acquired2);

            await _coordinator.ReleaseLockAsync("PC-101");
            bool acquired3 = await _coordinator.AcquireLockAsync("PC-101", "Shutdown");
            Assert.True(acquired3);
        }

        [Fact]
        public async Task Enterprise_Operation_Summaries_Retrieve_Correctly()
        {
            await _dbService.InitializeDatabaseAsync();

            var pc1Meta = new Dictionary<string, string> { { "CpuUsage", "10" }, { "RamUsage", "4096" }, { "GpuUsage", "20" }, { "Version", "1.0.0" } };
            var pc2Meta = new Dictionary<string, string> { { "CpuUsage", "20" }, { "RamUsage", "8192" }, { "GpuUsage", "40" }, { "Version", "1.0.0" } };

            await _fleetManager.RegisterWorkstationAsync("PC-1", pc1Meta);
            await _fleetManager.RegisterWorkstationAsync("PC-2", pc2Meta);

            var resources = await _enterpriseService.GetFleetResourceUsageSummaryAsync();
            Assert.Equal(15.0, (double)resources["AvgCpuUsage"]);
            Assert.Equal(6144.0, (double)resources["AvgRamUsage"]);
            Assert.Equal(30.0, (double)resources["AvgGpuUsage"]);

            var health = await _enterpriseService.GetFleetHealthSummaryAsync();
            Assert.Equal(2, (int)health["OnlineCount"]);
        }
    }
}
