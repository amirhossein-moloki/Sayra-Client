using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Sayra.Client.Shared.DependencyInjection;
using Sayra.Client.Shared.Fleet.Infrastructure;
using Sayra.Client.Shared.Fleet.Interfaces;
using Sayra.Client.Shared.Fleet.Services;
using Sayra.Client.Shared.Models.Phase9.Domain;
using Sayra.Client.Shared.Models.Phase9.Enums;
using Sayra.Client.Shared.Models.Phase9.Events;
using Sayra.Client.Shared.Interfaces;
using Xunit;

namespace Sayra.Client.Configuration.Tests.Phase9
{
    public class FleetManagementTests : IDisposable
    {
        private readonly string _testDbDir;
        private readonly string _testDbPath;
        private readonly FleetDatabaseContext _dbContext;
        private readonly Mock<IEventDispatcher> _eventDispatcherMock;

        private readonly MachineRepository _machineRepo;
        private readonly GroupRepository _groupRepo;
        private readonly TagRepository _tagRepo;
        private readonly SnapshotRepository _snapshotRepo;
        private readonly HealthRepository _healthRepo;
        private readonly InventoryRepository _inventoryRepo;
        private readonly FleetCache _cache;
        private readonly FleetManager _fleetManager;

        public FleetManagementTests()
        {
            _testDbDir = Path.Combine(AppContext.BaseDirectory, "FleetTestData", Guid.NewGuid().ToString());
            Directory.CreateDirectory(_testDbDir);
            _testDbPath = Path.Combine(_testDbDir, "fleet_test.db");

            Environment.SetEnvironmentVariable("SAYRA_TEST_DB_PATH", _testDbPath);

            _dbContext = new FleetDatabaseContext(NullLogger<FleetDatabaseContext>.Instance);
            _dbContext.InitializeDatabaseAsync().GetAwaiter().GetResult();

            _eventDispatcherMock = new Mock<IEventDispatcher>();

            _machineRepo = new MachineRepository(_dbContext);
            _groupRepo = new GroupRepository(_dbContext);
            _tagRepo = new TagRepository(_dbContext);
            _snapshotRepo = new SnapshotRepository(_dbContext);
            _healthRepo = new HealthRepository(_dbContext);
            _inventoryRepo = new InventoryRepository(_dbContext);

            _cache = new FleetCache(
                _machineRepo,
                _groupRepo,
                _snapshotRepo,
                _healthRepo,
                _inventoryRepo,
                NullLogger<FleetCache>.Instance);

            _fleetManager = new FleetManager(
                _machineRepo,
                _groupRepo,
                _tagRepo,
                _cache,
                _eventDispatcherMock.Object,
                NullLogger<FleetManager>.Instance);
        }

        public void Dispose()
        {
            _dbContext.Dispose();
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
        public async Task Machine_Registration_SavesToDbAndCache_AndDispatchesEvents()
        {
            // Arrange
            var machine = new MachineInfo
            {
                MachineId = "WS-101",
                Hostname = "GamerPC-101",
                IpAddress = "192.168.1.101",
                MacAddress = "00:11:22:33:44:55",
                Status = MachineStatus.Online,
                HealthStatus = MachineHealthStatus.Healthy,
                Version = new MachineVersion { SemVer = "1.0.0" }
            };

            // Act
            bool result = await _fleetManager.RegisterMachineAsync(machine);

            // Assert
            Assert.True(result);

            // Verify Repository / DB
            var dbMachine = await _machineRepo.GetAsync("WS-101");
            Assert.NotNull(dbMachine);
            Assert.Equal("GamerPC-101", dbMachine.Hostname);

            // Verify Cache
            var cachedMachine = _cache.GetMachine("WS-101");
            Assert.NotNull(cachedMachine);
            Assert.Equal("GamerPC-101", cachedMachine.Hostname);

            // Verify Dispatch Events
            _eventDispatcherMock.Verify(d => d.Dispatch(It.Is<MachineRegistered>(e => e.MachineId == "WS-101")), Times.Once);
            _eventDispatcherMock.Verify(d => d.Dispatch(It.Is<MachineOnline>(e => e.MachineId == "WS-101")), Times.Once);
        }

        [Fact]
        public async Task DuplicateDetection_ThrowsException_ForDuplicateMacAddress()
        {
            // Arrange
            var machine1 = new MachineInfo
            {
                MachineId = "WS-101",
                Hostname = "GamerPC-101",
                IpAddress = "192.168.1.101",
                MacAddress = "00:11:22:33:44:55",
                Status = MachineStatus.Online,
                Version = new MachineVersion { SemVer = "1.0.0" }
            };

            var machine2 = new MachineInfo
            {
                MachineId = "WS-102",
                Hostname = "GamerPC-102",
                IpAddress = "192.168.1.102",
                MacAddress = "00:11:22:33:44:55", // Same MAC
                Status = MachineStatus.Online,
                Version = new MachineVersion { SemVer = "1.0.0" }
            };

            // Act
            await _fleetManager.RegisterMachineAsync(machine1);

            // Assert Duplicate throws
            await Assert.ThrowsAsync<InvalidOperationException>(() => _fleetManager.RegisterMachineAsync(machine2));
        }

        [Fact]
        public async Task DynamicGroup_AutomaticallyEvaluatesWorkstations()
        {
            // Arrange
            var dynamicGroup = new FleetGroup
            {
                GroupId = "GRP-RTX",
                Name = "RTX Gaming PCs",
                GroupType = FleetGroupType.Dynamic,
                DynamicRuleExpression = "GPU == RTX4090"
            };

            await _fleetManager.CreateGroupAsync(dynamicGroup);

            var machineMatch = new MachineInfo
            {
                MachineId = "WS-RTX",
                Hostname = "RTX-Monster",
                IpAddress = "192.168.1.200",
                MacAddress = "00:aa:bb:cc:dd:ee",
                Status = MachineStatus.Online,
                Version = new MachineVersion { SemVer = "1.0.0" },
                Inventory = new MachineInventory { GpuName = "NVIDIA GeForce RTX4090", RamGb = 32 }
            };

            var machineNoMatch = new MachineInfo
            {
                MachineId = "WS-GTX",
                Hostname = "GTX-Budget",
                IpAddress = "192.168.1.201",
                MacAddress = "00:aa:bb:cc:dd:ff",
                Status = MachineStatus.Online,
                Version = new MachineVersion { SemVer = "1.0.0" },
                Inventory = new MachineInventory { GpuName = "NVIDIA GeForce GTX1660", RamGb = 16 }
            };

            // Act
            await _fleetManager.RegisterMachineAsync(machineMatch);
            await _fleetManager.RegisterMachineAsync(machineNoMatch);

            // Assert memberships
            var members = await _fleetManager.GetGroupMembersAsync("GRP-RTX");
            Assert.Single(members);
            Assert.Equal("WS-RTX", members[0].MachineId);
        }

        [Fact]
        public async Task SearchEngine_CorrectlyFiltersAndSorts_WithWildcards()
        {
            // Arrange
            var searchEngine = new FleetSearchEngine(_dbContext, NullLogger<FleetSearchEngine>.Instance);

            var machine1 = new MachineInfo
            {
                MachineId = "WS-A",
                Hostname = "Alpha-PC",
                IpAddress = "192.168.1.10",
                MacAddress = "11:22:33:44:55:66",
                Status = MachineStatus.Online,
                HealthStatus = MachineHealthStatus.Healthy,
                Version = new MachineVersion { SemVer = "1.0.0" },
                Inventory = new MachineInventory { CpuName = "Core i9", GpuName = "RTX4090", RamGb = 64 }
            };

            var machine2 = new MachineInfo
            {
                MachineId = "WS-B",
                Hostname = "Beta-PC",
                IpAddress = "192.168.1.11",
                MacAddress = "11:22:33:44:55:77",
                Status = MachineStatus.Offline,
                HealthStatus = MachineHealthStatus.Warning,
                Version = new MachineVersion { SemVer = "1.1.0" },
                Inventory = new MachineInventory { CpuName = "Core i5", GpuName = "GTX1660", RamGb = 16 }
            };

            await _fleetManager.RegisterMachineAsync(machine1);
            await _fleetManager.RegisterMachineAsync(machine2);

            // Act & Assert 1: Search by Hostname wildcard (contains "PC")
            var result = await searchEngine.SearchAsync(new SearchParameters { Hostname = "PC", SortBy = "Hostname", SortDescending = true });
            Assert.Equal(2, result.TotalCount);
            Assert.Equal("WS-B", result.Items[0].MachineId); // Beta-PC comes first descending

            // Act & Assert 2: Filter by Status Online
            var resultOnline = await searchEngine.SearchAsync(new SearchParameters { Status = "Online" });
            Assert.Single(resultOnline.Items);
            Assert.Equal("WS-A", resultOnline.Items[0].MachineId);
        }

        [Fact]
        public async Task Synchronization_ResolvesConflicts_UsingLastWriteWins()
        {
            // Arrange
            var syncService = new FleetSynchronizationService(
                _machineRepo,
                _snapshotRepo,
                _inventoryRepo,
                _healthRepo,
                _cache,
                _eventDispatcherMock.Object,
                NullLogger<FleetSynchronizationService>.Instance);

            var baseDate = DateTime.UtcNow;

            var localState = new MachineInfo
            {
                MachineId = "WS-SYN",
                Hostname = "LocalPC",
                IpAddress = "192.168.1.5",
                MacAddress = "aa:bb:cc:dd:ee:11",
                LastSeenUtc = baseDate,
                Version = new MachineVersion { SemVer = "1.0.0" }
            };

            var serverState = new MachineInfo
            {
                MachineId = "WS-SYN",
                Hostname = "ServerPC",
                IpAddress = "192.168.1.6",
                MacAddress = "aa:bb:cc:dd:ee:11",
                LastSeenUtc = baseDate.AddMinutes(10), // Newer!
                Version = new MachineVersion { SemVer = "1.1.0" }
            };

            // Act
            await _fleetManager.RegisterMachineAsync(localState);
            bool synced = await syncService.SynchronizeMachineStateAsync(localState, serverState);

            // Assert
            Assert.True(synced);
            var finalState = await _fleetManager.GetMachineAsync("WS-SYN");
            Assert.NotNull(finalState);
            Assert.Equal("ServerPC", finalState.Hostname); // Resolved to server state
            Assert.Equal("192.168.1.6", finalState.IpAddress);

            _eventDispatcherMock.Verify(d => d.Dispatch(It.Is<VersionChanged>(e => e.MachineId == "WS-SYN")), Times.Once);
        }

        [Fact]
        public async Task Cache_GetAndSet_IsIsolatedAndFast()
        {
            // Arrange
            var machine = new MachineInfo
            {
                MachineId = "WS-CACHE",
                Hostname = "CachedPC",
                IpAddress = "192.168.1.80",
                MacAddress = "00:11:22:33:99:99",
                Status = MachineStatus.Online,
                Version = new MachineVersion { SemVer = "1.0.0" }
            };

            // Act
            _cache.SetMachine(machine);
            var cached = _cache.GetMachine("WS-CACHE");

            // Assert
            Assert.NotNull(cached);
            Assert.Equal("CachedPC", cached.Hostname);

            // Invalidate
            _cache.InvalidateMachine("WS-CACHE");
            Assert.Null(_cache.GetMachine("WS-CACHE"));
        }

        [Fact]
        public async Task ThreadSafety_SupportsHighConcurrentRegistration_WithoutDeadlocks()
        {
            // Arrange
            var tasks = new List<Task>();
            int concurrentCount = 10;

            // Act & Assert
            for (int i = 0; i < concurrentCount; i++)
            {
                int index = i;
                tasks.Add(Task.Run(async () =>
                {
                    var machine = new MachineInfo
                    {
                        MachineId = $"WS-CONC-{index}",
                        Hostname = $"ConcurrentPC-{index}",
                        IpAddress = $"192.168.1.{10 + index}",
                        MacAddress = $"aa:bb:cc:dd:ee:{index:D2}",
                        Status = MachineStatus.Online,
                        Version = new MachineVersion { SemVer = "1.0.0" }
                    };

                    bool registered = await _fleetManager.RegisterMachineAsync(machine);
                    Assert.True(registered);
                }));
            }

            await Task.WhenAll(tasks);

            var all = await _fleetManager.GetAllMachinesAsync();
            Assert.Equal(concurrentCount, all.Count);
        }
    }
}
