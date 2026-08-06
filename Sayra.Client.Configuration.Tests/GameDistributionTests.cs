using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Console;
using Sayra.Client.Shared.GameDistribution.BlockStorage.Interfaces;
using Sayra.Client.Shared.GameDistribution.BlockStorage.Models;
using Sayra.Client.Shared.GameDistribution.BlockStorage.Services;
using Sayra.Client.Shared.GameDistribution.Cache.Interfaces;
using Sayra.Client.Shared.GameDistribution.Cache.Models;
using Sayra.Client.Shared.GameDistribution.Cache.Services;
using Sayra.Client.Shared.GameDistribution.Discovery.Interfaces;
using Sayra.Client.Shared.GameDistribution.Discovery.Services;
using Sayra.Client.Shared.GameDistribution.Optimization.Interfaces;
using Sayra.Client.Shared.GameDistribution.Optimization.Services;
using Sayra.Client.Shared.GameDistribution.Repair.Interfaces;
using Sayra.Client.Shared.GameDistribution.Repair.Services;
using Sayra.Client.Shared.GameDistribution.Selection.Interfaces;
using Sayra.Client.Shared.GameDistribution.Selection.Services;
using Sayra.Client.Shared.GameDistribution.Services;
using Sayra.Client.Shared.GameDistribution.Transfer.Interfaces;
using Sayra.Client.Shared.GameDistribution.Transfer.Services;
using Sayra.Client.Shared.UpdatePlatform.Application.Interfaces;
using Sayra.Client.Shared.UpdatePlatform.Application.Services;
using Xunit;

namespace Sayra.Client.Configuration.Tests
{
    public class GameDistributionTests
    {
        private readonly IContentHasher _hasher;
        private readonly IBlockRepository _blockRepository;
        private readonly IBlockStorageService _storageService;
        private readonly IDistributedCacheManager _cacheManager;
        private readonly ICacheNodeSelector _nodeSelector;
        private readonly IPeerDiscoveryService _discoveryService;
        private readonly IPeerTransferService _transferService;
        private readonly IBandwidthLimiter _bandwidthLimiter;
        private readonly IGameRepairService _repairService;
        private readonly ICacheOptimizationService _optimizationService;

        private readonly ILoggerFactory _loggerFactory;

        public GameDistributionTests()
        {
            _loggerFactory = LoggerFactory.Create(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug));

            _hasher = new ContentHasher();
            _blockRepository = new InMemoryBlockRepository();
            _storageService = new BlockStorageService(_blockRepository, _hasher, _loggerFactory.CreateLogger<BlockStorageService>());
            _cacheManager = new DistributedCacheManager();
            _nodeSelector = new CacheNodeSelector();
            _bandwidthLimiter = new BandwidthLimiter();
            _bandwidthLimiter.SetLimit(0); // Unthrottled for test performance
            _discoveryService = new PeerDiscoveryService(_cacheManager, _loggerFactory.CreateLogger<PeerDiscoveryService>());
            _transferService = new PeerTransferService(_storageService, _cacheManager, _nodeSelector, _bandwidthLimiter, _loggerFactory.CreateLogger<PeerTransferService>());
            _repairService = new GameRepairService(_storageService, _cacheManager, _transferService, _loggerFactory.CreateLogger<GameRepairService>());
            _optimizationService = new CacheOptimizationService(_cacheManager, _storageService, _blockRepository, _loggerFactory.CreateLogger<CacheOptimizationService>());
        }

        [Fact]
        public async Task Stage1_BlockSplittingAndHashing_ShouldCreateBlocksCorrectly()
        {
            // Arrange
            string tempFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.bin");
            byte[] fileData = new byte[1024 * 1024 * 5 + 500]; // 5.5MB file
            Random.Shared.NextBytes(fileData);
            await File.WriteAllBytesAsync(tempFile, fileData);

            try
            {
                // Act
                var blocks = (await _storageService.SplitFileIntoBlocksAsync(
                    tempFile, "game-val", "pkg-01", "1.2.0", 1024 * 1024)).ToList();

                // Assert
                Assert.Equal(6, blocks.Count); // 5 blocks of 1MB + 1 remaining block
                Assert.Equal(1024 * 1024, blocks[0].Size);
                Assert.Equal(500, blocks[5].Size);

                foreach (var b in blocks)
                {
                    Assert.True(await _storageService.VerifyBlockAsync(b.BlockId));
                }
            }
            finally
            {
                if (File.Exists(tempFile)) File.Delete(tempFile);
            }
        }

        [Fact]
        public async Task Stage2_DistributedCacheManager_ShouldStoreAndTrackNodeData()
        {
            // Arrange
            var entry = new GameCacheEntry
            {
                GameId = "g1",
                CompletedBlocks = 10,
                TotalBlocks = 10,
                IsHealthy = true
            };

            var node = new CacheNode
            {
                NodeId = "node-01",
                Hostname = "PC-01",
                IsOnline = true,
                LastSeenUtc = DateTime.UtcNow
            };

            var avail = new BlockAvailability
            {
                NodeId = "node-01",
                BlockId = "b1",
                IsAvailable = true
            };

            // Act
            await _cacheManager.SaveGameEntryAsync(entry);
            await _cacheManager.SaveNodeAsync(node);
            await _cacheManager.SaveBlockAvailabilityAsync(avail);

            var retrievedNode = await _cacheManager.GetNodeAsync("node-01");
            var nodesWithBlock = (await _cacheManager.GetNodesWithBlockAsync("b1")).ToList();

            // Assert
            Assert.NotNull(retrievedNode);
            Assert.Equal("PC-01", retrievedNode.Hostname);
            Assert.Single(nodesWithBlock);
            Assert.Equal("node-01", nodesWithBlock[0].NodeId);
        }

        [Fact]
        public async Task Stage3_PeerDiscovery_ShouldBroadcastHeartbeat()
        {
            // Arrange
            var selfNode = new CacheNode
            {
                NodeId = "test-node",
                Hostname = "TestPC",
                IsOnline = true
            };

            // Act
            await _discoveryService.StartDiscoveryAsync();
            await _discoveryService.BroadcastHeartbeatAsync(selfNode);
            var retrieved = await _cacheManager.GetNodeAsync("test-node");

            // Assert
            Assert.NotNull(retrieved);
            Assert.Equal("TestPC", retrieved.Hostname);

            await _discoveryService.StopDiscoveryAsync();
        }

        [Fact]
        public async Task Stage4And10_SecureBlockTransfer_ShouldSucceedWithTlsAndHmac()
        {
            // Arrange
            int port = 11300 + Random.Shared.Next(0, 100);
            await _transferService.StartListenerAsync("127.0.0.1", port);

            var blockId = "block_test_123";
            byte[] blockData = new byte[1024];
            Random.Shared.NextBytes(blockData);

            await _storageService.SaveBlockBytesAsync(blockId, blockData);

            var metadata = new ContentBlock
            {
                BlockId = blockId,
                Sha256Hash = _hasher.ComputeHash(blockData),
                Size = blockData.Length,
                GameId = "g-test"
            };
            await _blockRepository.SaveAsync(metadata);

            var peerNode = new CacheNode
            {
                NodeId = "peer-01",
                IpAddress = "127.0.0.1",
                Port = port,
                IsOnline = true,
                LastSeenUtc = DateTime.UtcNow
            };

            await _cacheManager.SaveNodeAsync(peerNode);
            await _cacheManager.SaveBlockAvailabilityAsync(new BlockAvailability
            {
                NodeId = "peer-01",
                BlockId = blockId,
                IsAvailable = true
            });

            try
            {
                // Act
                byte[] transferred = await _transferService.TransferBlockAsync(peerNode, blockId);

                // Assert
                Assert.Equal(blockData, transferred);
            }
            finally
            {
                await _transferService.StopListenerAsync();
            }
        }

        [Fact]
        public void Stage5_CacheNodeSelector_ShouldSelectBestPriorityNode()
        {
            // Arrange
            var nodeLow = new CacheNode
            {
                NodeId = "node-low",
                IsSsd = false,
                FreeStorageBytes = 1024L * 1024 * 1024, // 1GB
                NetworkSpeedMbps = 10,
                CpuLoadPercent = 90,
                CacheCompletenessPercent = 10,
                HealthScore = 50
            };

            var nodeHigh = new CacheNode
            {
                NodeId = "node-high",
                IsSsd = true,
                FreeStorageBytes = 100L * 1024 * 1024 * 1024, // 100GB
                NetworkSpeedMbps = 1000,
                CpuLoadPercent = 10,
                CacheCompletenessPercent = 99,
                HealthScore = 100
            };

            var nodesList = new List<CacheNode> { nodeLow, nodeHigh };

            // Act
            var selected = _nodeSelector.SelectBestNode(nodesList);

            // Assert
            Assert.NotNull(selected);
            Assert.Equal("node-high", selected.NodeId);
        }

        [Fact]
        public async Task Stage8_GameRepair_ShouldSuccessfullyReplaceCorruptedBlock()
        {
            // Arrange
            int port = 11400 + Random.Shared.Next(0, 100);

            // Isolate Peer's own storage so it holds the healthy block independently of the client
            var peerRepository = new InMemoryBlockRepository();
            var peerStorage = new BlockStorageService(peerRepository, _hasher, _loggerFactory.CreateLogger<BlockStorageService>());
            var peerTransfer = new PeerTransferService(peerStorage, _cacheManager, _nodeSelector, _bandwidthLimiter, _loggerFactory.CreateLogger<PeerTransferService>());

            await peerTransfer.StartListenerAsync("127.0.0.1", port);

            var blockId = "block_repair_test";
            byte[] healthyBytes = Encoding.UTF8.GetBytes("healthy_block_data_123");
            byte[] corruptBytes = Encoding.UTF8.GetBytes("corrupted_block_data_!!!");

            // Client-side metadata
            var metadata = new ContentBlock
            {
                BlockId = blockId,
                Sha256Hash = _hasher.ComputeHash(healthyBytes),
                Size = healthyBytes.Length,
                GameId = "g-repair"
            };
            await _blockRepository.SaveAsync(metadata);

            // Client has corrupted block locally
            await _storageService.SaveBlockBytesAsync(blockId, corruptBytes);

            // Peer has healthy block metadata and files
            await peerRepository.SaveAsync(metadata);
            await peerStorage.SaveBlockBytesAsync(blockId, healthyBytes);

            // Register peer node
            var peerNode = new CacheNode
            {
                NodeId = "peer-repair",
                IpAddress = "127.0.0.1",
                Port = port,
                IsOnline = true,
                LastSeenUtc = DateTime.UtcNow
            };
            await _cacheManager.SaveNodeAsync(peerNode);
            await _cacheManager.SaveBlockAvailabilityAsync(new BlockAvailability
            {
                NodeId = "peer-repair",
                BlockId = blockId,
                IsAvailable = true
            });

            try
            {
                // Act
                bool outcome = await _repairService.RepairGameAsync("g-repair", new List<ContentBlock> { metadata });

                // Assert
                Assert.True(outcome);
                byte[] repairedBytes = await _storageService.GetBlockBytesAsync(blockId);
                Assert.Equal(healthyBytes, repairedBytes);
            }
            finally
            {
                await peerTransfer.StopListenerAsync();
            }
        }

        [Fact]
        public async Task Stage9_CacheOptimization_ShouldEvictLRUBlocksUnderPressure()
        {
            // Arrange
            var gameOld = new GameCacheEntry { GameId = "game-old", LastUsedUtc = DateTime.UtcNow.AddDays(-10) };
            var gameNew = new GameCacheEntry { GameId = "game-new", LastUsedUtc = DateTime.UtcNow };

            await _cacheManager.SaveGameEntryAsync(gameOld);
            await _cacheManager.SaveGameEntryAsync(gameNew);

            var bOld = new ContentBlock { BlockId = "b-old", Size = 100, GameId = "game-old" };
            var bNew = new ContentBlock { BlockId = "b-new", Size = 100, GameId = "game-new" };

            await _blockRepository.SaveAsync(bOld);
            await _blockRepository.SaveAsync(bNew);

            await _storageService.SaveBlockBytesAsync("b-old", new byte[100]);
            await _storageService.SaveBlockBytesAsync("b-new", new byte[100]);

            // Act
            // Force optimizer to see large required space to trigger eviction
            await _optimizationService.OptimizeCacheAsync(long.MaxValue);

            // Assert
            // Old block should have been evicted/deleted, new block might also be depending on space,
            // but old should definitely go first.
            var isOldSaved = await _storageService.VerifyBlockAsync("b-old");
            Assert.False(isOldSaved);
        }

        [Fact]
        public async Task Stage12_StressAndConcurrency_ShouldScaleTo100MockPeersWithoutLockDeadlocks()
        {
            // Arrange
            var tasks = new List<Task>();
            for (int i = 0; i < 100; i++)
            {
                int index = i;
                tasks.Add(Task.Run(async () =>
                {
                    var node = new CacheNode
                    {
                        NodeId = $"stress-node-{index}",
                        Hostname = $"StressPC-{index}",
                        IsOnline = true,
                        LastSeenUtc = DateTime.UtcNow
                    };
                    await _cacheManager.SaveNodeAsync(node);

                    var avail = new BlockAvailability
                    {
                        NodeId = $"stress-node-{index}",
                        BlockId = $"b-{index % 10}",
                        IsAvailable = true
                    };
                    await _cacheManager.SaveBlockAvailabilityAsync(avail);
                }));
            }

            // Act
            await Task.WhenAll(tasks);

            // Assert
            var online = (await _cacheManager.GetOnlineNodesAsync()).ToList();
            Assert.Equal(100, online.Count);
        }
    }
}
