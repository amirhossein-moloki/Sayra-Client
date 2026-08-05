using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Sayra.Client.Shared.Fleet.Assets;
using Sayra.Client.Shared.Fleet.Assets.Collectors;
using Sayra.Client.Shared.Fleet.Assets.Interfaces;
using Sayra.Client.Shared.Fleet.Assets.Services;
using Sayra.Client.Shared.Fleet.Infrastructure;
using Sayra.Client.Shared.Fleet.Maintenance;
using Sayra.Client.Shared.Fleet.Maintenance.Interfaces;
using Sayra.Client.Shared.Fleet.Maintenance.Services;
using Sayra.Client.Shared.Interfaces;
using Sayra.Client.Shared.Interfaces.Phase9;
using Sayra.Client.Shared.Models.Phase9.Domain;
using Sayra.Client.Shared.Models.Phase9.Enums;
using Sayra.Client.Shared.Models.Phase9.Events;
using Xunit;

namespace Sayra.Client.Configuration.Tests.Phase9
{
    public class AssetAndMaintenanceTests : IDisposable
    {
        private readonly string _testDbDir;
        private readonly string _testDbPath;
        private readonly FleetDatabaseContext _dbContext;
        private readonly Mock<IEventDispatcher> _eventDispatcherMock;

        private readonly IAssetRepository _assetRepository;
        private readonly IMaintenanceRepository _maintenanceRepository;

        private readonly IAssetCollector _hardwareCollector;
        private readonly IAssetCollector _softwareCollector;
        private readonly IAssetCollector _driverCollector;
        private readonly IAssetCollector _biosCollector;
        private readonly IAssetCollector _firmwareCollector;
        private readonly IAssetCollector _storageCollector;
        private readonly IAssetCollector _networkCollector;

        private readonly List<IAssetCollector> _allCollectors;
        private readonly AssetDiscoveryEngine _discoveryEngine;
        private readonly AssetManagementService _assetManagementService;

        private readonly MaintenanceScheduler _maintenanceScheduler;
        private readonly MaintenanceService _maintenanceService;

        public AssetAndMaintenanceTests()
        {
            _testDbDir = Path.Combine(AppContext.BaseDirectory, "AssetMaintenanceTestData", Guid.NewGuid().ToString());
            Directory.CreateDirectory(_testDbDir);
            _testDbPath = Path.Combine(_testDbDir, "asset_maintenance_test.db");

            Environment.SetEnvironmentVariable("SAYRA_TEST_DB_PATH", _testDbPath);

            _dbContext = new FleetDatabaseContext(NullLogger<FleetDatabaseContext>.Instance);
            _dbContext.InitializeDatabaseAsync().GetAwaiter().GetResult();

            _eventDispatcherMock = new Mock<IEventDispatcher>();

            _assetRepository = new AssetRepository(_dbContext);
            _maintenanceRepository = new MaintenanceRepository(_dbContext);

            // Instantiate independent collectors
            _hardwareCollector = new HardwareInventoryCollector(NullLogger<HardwareInventoryCollector>.Instance);
            _softwareCollector = new SoftwareInventoryCollector(NullLogger<SoftwareInventoryCollector>.Instance);
            _driverCollector = new DriverInventoryCollector(NullLogger<DriverInventoryCollector>.Instance);
            _biosCollector = new BIOSInventoryCollector(NullLogger<BIOSInventoryCollector>.Instance);
            _firmwareCollector = new FirmwareInventoryCollector(NullLogger<FirmwareInventoryCollector>.Instance);
            _storageCollector = new StorageInventoryCollector(NullLogger<StorageInventoryCollector>.Instance);
            _networkCollector = new NetworkInventoryCollector(NullLogger<NetworkInventoryCollector>.Instance);

            _allCollectors = new List<IAssetCollector>
            {
                _hardwareCollector, _softwareCollector, _driverCollector,
                _biosCollector, _firmwareCollector, _storageCollector, _networkCollector
            };

            _discoveryEngine = new AssetDiscoveryEngine(
                NullLogger<AssetDiscoveryEngine>.Instance,
                _assetRepository,
                _allCollectors
            );

            _assetManagementService = new AssetManagementService(
                NullLogger<AssetManagementService>.Instance,
                _assetRepository
            );

            _maintenanceScheduler = new MaintenanceScheduler(
                NullLogger<MaintenanceScheduler>.Instance,
                _maintenanceRepository,
                _eventDispatcherMock.Object
            );

            _maintenanceService = new MaintenanceService(
                NullLogger<MaintenanceService>.Instance,
                _maintenanceRepository,
                _eventDispatcherMock.Object
            );
        }

        #region Asset Discovery & Collector Tests

        [Fact]
        public async Task Collectors_Run_Independently_And_Return_Structured_Data()
        {
            var ct = CancellationToken.None;
            var machineId = "WS-001";

            var hwAssets = await _hardwareCollector.CollectAssetsAsync(machineId, ct);
            var swAssets = await _softwareCollector.CollectAssetsAsync(machineId, ct);
            var drvAssets = await _driverCollector.CollectAssetsAsync(machineId, ct);
            var biosAssets = await _biosCollector.CollectAssetsAsync(machineId, ct);
            var fwAssets = await _firmwareCollector.CollectAssetsAsync(machineId, ct);
            var stAssets = await _storageCollector.CollectAssetsAsync(machineId, ct);
            var netAssets = await _networkCollector.CollectAssetsAsync(machineId, ct);

            Assert.NotEmpty(hwAssets);
            Assert.NotEmpty(swAssets);
            Assert.NotEmpty(drvAssets);
            Assert.NotEmpty(biosAssets);
            Assert.NotEmpty(fwAssets);
            Assert.NotEmpty(stAssets);
            Assert.NotEmpty(netAssets);

            Assert.All(hwAssets, a => Assert.Equal(machineId, a.MachineId));
            Assert.All(swAssets, a => Assert.Equal(AssetType.Software, a.Category));
        }

        [Fact]
        public async Task AssetDiscoveryEngine_Performs_Initial_Discovery_Successfully()
        {
            var machineId = "WS-002";

            // Act: First scan (Initial Discovery)
            var discovered = await _discoveryEngine.DiscoverAssetsAsync(machineId, isIncremental: false);

            Assert.NotEmpty(discovered);

            // Verify assets saved in DB
            var dbAssets = await _assetRepository.GetAssetsByMachineAsync(machineId);
            Assert.Equal(discovered.Count, dbAssets.Count);

            // Verify History: FirstSeen event recorded
            var history = await _assetRepository.GetHistoryAsync(machineId: machineId);
            Assert.Contains(history, h => h.EventType == "FirstSeen");
        }

        [Fact]
        public async Task AssetDiscoveryEngine_Resolves_Duplicates_On_Scans()
        {
            var machineId = "WS-003";

            // Setup collectors with duplicates
            var dupCollectorMock = new Mock<IAssetCollector>();
            var duplicateAssets = new List<AssetRecord>
            {
                new() { AssetId = "A1", MachineId = machineId, Name = "D1", SerialOrSignature = "S1", Category = AssetType.Software },
                new() { AssetId = "A2", MachineId = machineId, Name = "D1 Duplicate", SerialOrSignature = "S1", Category = AssetType.Software } // Duplicate serial
            };
            dupCollectorMock.Setup(c => c.CollectAssetsAsync(machineId, It.IsAny<CancellationToken>())).ReturnsAsync(duplicateAssets);

            var customDiscovery = new AssetDiscoveryEngine(
                NullLogger<AssetDiscoveryEngine>.Instance,
                _assetRepository,
                new[] { dupCollectorMock.Object }
            );

            // Act
            var results = await customDiscovery.DiscoverAssetsAsync(machineId, isIncremental: false);

            // Assert: Only 1 unique asset remains
            Assert.Single(results);
            Assert.Equal("A1", results[0].AssetId);
        }

        #endregion

        #region Change Detection & History Tests

        [Fact]
        public async Task AssetDiscoveryEngine_Detects_Changes_And_Records_History_And_Changes()
        {
            var machineId = "WS-004";

            // 1. Initial Scan
            var firstScan = await _discoveryEngine.DiscoverAssetsAsync(machineId, isIncremental: false);
            var targetAsset = firstScan.First();

            // 2. Modify properties in repository to simulate a change on subsequent scan
            var modifiedSpecs = new Dictionary<string, string>(targetAsset.Specifications);
            modifiedSpecs["Version"] = "New-Version-XYZ";
            modifiedSpecs["Manufacturer"] = "New-Brand";
            var updatedAsset = targetAsset with { Specifications = modifiedSpecs };

            // Setup mock collector that returns the modified asset
            var singleCollectorMock = new Mock<IAssetCollector>();
            singleCollectorMock.Setup(c => c.CollectAssetsAsync(machineId, It.IsAny<CancellationToken>()))
                               .ReturnsAsync(new[] { updatedAsset });

            var engine = new AssetDiscoveryEngine(
                NullLogger<AssetDiscoveryEngine>.Instance,
                _assetRepository,
                new[] { singleCollectorMock.Object }
            );

            // Act: Run discovery again
            await engine.DiscoverAssetsAsync(machineId, isIncremental: false);

            // Assert: Change recorded in repository
            var changes = await _assetRepository.GetChangesAsync(machineId: machineId);
            Assert.NotEmpty(changes);
            Assert.Contains(changes, c => c.PropertyName == "Version" && c.NewValue == "New-Version-XYZ");
            Assert.Contains(changes, c => c.PropertyName == "Manufacturer" && c.NewValue == "New-Brand");

            // Assert History updated
            var history = await _assetRepository.GetHistoryAsync(machineId: machineId);
            Assert.Contains(history, h => h.EventType == "Changes");
        }

        #endregion

        #region Asset Repository Search Tests

        [Fact]
        public async Task SearchAssetsAsync_Applies_Filters_Sorting_And_Pagination()
        {
            var machineId = "WS-SEARCH";

            // Add test assets
            var asset1 = new AssetRecord
            {
                AssetId = "A1", MachineId = machineId, Name = "Alpha", SerialOrSignature = "S-01",
                Category = AssetType.Software, Status = AssetStatus.Active,
                Specifications = new Dictionary<string, string> { { "Manufacturer", "Corsair" }, { "Version", "1.0.0" } }
            };
            var asset2 = new AssetRecord
            {
                AssetId = "A2", MachineId = machineId, Name = "Beta", SerialOrSignature = "S-02",
                Category = AssetType.Software, Status = AssetStatus.Active,
                Specifications = new Dictionary<string, string> { { "Manufacturer", "AMD" }, { "Version", "2.0.0" } }
            };
            var asset3 = new AssetRecord
            {
                AssetId = "A3", MachineId = machineId, Name = "Gamma", SerialOrSignature = "S-03",
                Category = AssetType.Cpu, Status = AssetStatus.Active,
                Specifications = new Dictionary<string, string> { { "Manufacturer", "Intel" }, { "Version", "3.0.0" } }
            };

            await _assetRepository.SaveAssetAsync(asset1);
            await _assetRepository.SaveAssetAsync(asset2);
            await _assetRepository.SaveAssetAsync(asset3);

            // Act 1: Search by manufacturer
            var (items, total) = await _assetRepository.SearchAssetsAsync(machineId: machineId, manufacturer: "Intel");
            Assert.Single(items);
            Assert.Equal("A3", items[0].AssetId);
            Assert.Equal(1, total);

            // Act 2: Sort descending by name
            var (sortedItems, _) = await _assetRepository.SearchAssetsAsync(machineId: machineId, sortBy: "name", ascending: false);
            Assert.Equal(3, sortedItems.Count);
            Assert.Equal("Gamma", sortedItems[0].Name);
            Assert.Equal("Beta", sortedItems[1].Name);
            Assert.Equal("Alpha", sortedItems[2].Name);

            // Act 3: Pagination
            var (page1, _) = await _assetRepository.SearchAssetsAsync(machineId: machineId, pageIndex: 0, pageSize: 2);
            var (page2, _) = await _assetRepository.SearchAssetsAsync(machineId: machineId, pageIndex: 1, pageSize: 2);
            Assert.Equal(2, page1.Count);
            Assert.Single(page2);
        }

        #endregion

        #region Maintenance Scheduling Tests

        [Fact]
        public async Task ScheduleMaintenanceAsync_Registers_Window_And_Dispatches_Scheduled_Event()
        {
            var scheduleId = "SCH-100";
            var window = new MaintenanceWindow
            {
                WindowId = "W-100",
                Category = MaintenanceWindowType.SystemCleanup,
                StartTimeUtc = DateTime.UtcNow.AddHours(2),
                Duration = TimeSpan.FromHours(1),
                ForceSessionTermination = false
            };

            var schedule = new MaintenanceSchedule
            {
                ScheduleId = scheduleId,
                Window = window,
                ScopeFilter = "AllMachines",
                State = MaintenanceStatus.Scheduled,
                ExecutionSummary = "Pending start"
            };

            // Act
            var success = await _maintenanceScheduler.ScheduleMaintenanceAsync(schedule);

            // Assert
            Assert.True(success);
            var saved = await _maintenanceRepository.GetScheduleAsync(scheduleId);
            Assert.NotNull(saved);
            Assert.Equal(MaintenanceStatus.Scheduled, saved.State);

            // Verify event dispatched
            _eventDispatcherMock.Verify(d => d.Dispatch(It.Is<MaintenanceScheduled>(e => e.ScheduleId == scheduleId)), Times.Once);
        }

        [Fact]
        public async Task CancelScheduledMaintenanceAsync_Validates_State_And_Dispatches_Event()
        {
            var scheduleId = "SCH-200";
            var schedule = new MaintenanceSchedule
            {
                ScheduleId = scheduleId,
                Window = new MaintenanceWindow { WindowId = "W-200" },
                State = MaintenanceStatus.Scheduled
            };

            await _maintenanceRepository.SaveScheduleAsync(schedule);

            // Act
            var success = await _maintenanceScheduler.CancelScheduledMaintenanceAsync(scheduleId);

            // Assert
            Assert.True(success);
            var saved = await _maintenanceRepository.GetScheduleAsync(scheduleId);
            Assert.NotNull(saved);
            Assert.Equal(MaintenanceStatus.Cancelled, saved!.State);

            _eventDispatcherMock.Verify(d => d.Dispatch(It.Is<MaintenanceCancelled>(e => e.ScheduleId == scheduleId)), Times.Once);
        }

        [Fact]
        public async Task CancelScheduledMaintenanceAsync_Fails_On_Terminal_State()
        {
            var scheduleId = "SCH-300";
            var schedule = new MaintenanceSchedule
            {
                ScheduleId = scheduleId,
                Window = new MaintenanceWindow { WindowId = "W-300" },
                State = MaintenanceStatus.Completed // Terminal state
            };

            await _maintenanceRepository.SaveScheduleAsync(schedule);

            // Act
            var success = await _maintenanceScheduler.CancelScheduledMaintenanceAsync(scheduleId);

            // Assert
            Assert.False(success);
        }

        #endregion

        #region Maintenance State Machine Tests

        [Fact]
        public async Task ExecuteMaintenanceAsync_Executes_Full_State_Transitions_And_Saves_History()
        {
            var machineId = "WS-M1";
            var scheduleId = "SCH-400";
            var schedule = new MaintenanceSchedule
            {
                ScheduleId = scheduleId,
                Window = new MaintenanceWindow { WindowId = "W-400", Category = MaintenanceWindowType.Diagnostics },
                State = MaintenanceStatus.Scheduled
            };

            await _maintenanceRepository.SaveScheduleAsync(schedule);

            // Act
            var success = await _maintenanceService.ExecuteMaintenanceAsync(machineId, scheduleId);

            // Assert
            Assert.True(success);

            // Verify terminal state
            var dbSchedule = await _maintenanceRepository.GetScheduleAsync(scheduleId);
            Assert.NotNull(dbSchedule);
            Assert.Equal(MaintenanceStatus.Completed, dbSchedule!.State);

            // Verify event dispatches
            _eventDispatcherMock.Verify(d => d.Dispatch(It.Is<MaintenanceStarted>(e => e.ScheduleId == scheduleId)), Times.Once);
            _eventDispatcherMock.Verify(d => d.Dispatch(It.Is<MaintenanceCompleted>(e => e.ScheduleId == scheduleId)), Times.Once);

            // Verify executions created
            var executions = await _maintenanceRepository.GetExecutionsByScheduleAsync(scheduleId);
            Assert.NotEmpty(executions);
            Assert.Equal("Completed", executions.Last().Status);

            // Verify history recorded
            var history = await _maintenanceRepository.GetHistoryAsync();
            Assert.Contains(history, h => h.ScheduleId == scheduleId && h.OutcomeStatus == "Success");
        }

        #endregion

        #region Cache and Concurrency Tests

        [Fact]
        public void Cache_Providers_Handle_Set_Get_Expiration_And_Invalidation()
        {
            var assetId = "ASSET-CACHE-01";
            var asset = new AssetRecord { AssetId = assetId, Name = "Cached Asset", Category = AssetType.License };

            var assetCache = new AssetCache();

            // 1. Act: Add to cache with instant expiration
            assetCache.Set(assetId, asset, TimeSpan.FromMilliseconds(5));

            // Verify immediate get
            var cached = assetCache.Get(assetId);
            Assert.NotNull(cached);
            Assert.Equal("Cached Asset", cached.Name);

            // Wait for expiration
            Thread.Sleep(10);

            // Verify expired get returns null
            Assert.Null(assetCache.Get(assetId));

            // 2. Act: Set with long expiration and invalidate manually
            assetCache.Set(assetId, asset, TimeSpan.FromMinutes(1));
            Assert.NotNull(assetCache.Get(assetId));

            assetCache.Invalidate(assetId);
            Assert.Null(assetCache.Get(assetId));
        }

        [Fact]
        public async Task Cache_Providers_Support_Concurrent_Access_With_No_Deadlocks()
        {
            var inventoryCache = new InventoryCache();
            var tasks = new List<Task>();

            for (int i = 0; i < 50; i++)
            {
                int index = i;
                tasks.Add(Task.Run(() =>
                {
                    var id = $"WS-{index}";
                    var inv = new MachineInventory { CpuName = $"CPU-{index}", RamGb = 16 };

                    inventoryCache.Set(id, inv);
                    var cached = inventoryCache.Get(id);
                    Assert.NotNull(cached);

                    inventoryCache.Invalidate(id);
                }));
            }

            await Task.WhenAll(tasks);
        }

        #endregion

        public void Dispose()
        {
            _dbContext.CloseSafelyAsync().GetAwaiter().GetResult();
            _dbContext.Dispose();

            try
            {
                if (Directory.Exists(_testDbDir))
                {
                    Directory.Delete(_testDbDir, recursive: true);
                }
            }
            catch
            {
                // Suppress test file deletion warnings in locked OS environments
            }
        }
    }
}
