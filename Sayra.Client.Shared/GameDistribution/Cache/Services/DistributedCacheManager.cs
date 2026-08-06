using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sayra.Client.Shared.Fleet.Interfaces;
using Sayra.Client.Shared.GameDistribution.Cache.Interfaces;
using Sayra.Client.Shared.GameDistribution.Cache.Models;

namespace Sayra.Client.Shared.GameDistribution.Cache.Services
{
    public class DistributedCacheManager : IDistributedCacheManager
    {
        private readonly IFleetDatabaseContext? _dbContext;
        private readonly ILogger<DistributedCacheManager>? _logger;
        private readonly ConcurrentDictionary<string, GameCacheEntry> _gameEntries = new();
        private readonly ConcurrentDictionary<string, CacheNode> _nodes = new();
        private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, BlockAvailability>> _blockAvailability = new();

        public DistributedCacheManager(
            IFleetDatabaseContext? dbContext = null,
            ILogger<DistributedCacheManager>? logger = null)
        {
            _dbContext = dbContext;
            _logger = logger;

            InitializeFromDatabaseAsync().ConfigureAwait(false).GetAwaiter().GetResult();
        }

        private async Task InitializeFromDatabaseAsync()
        {
            if (_dbContext == null) return;

            try
            {
                using var conn = await _dbContext.CreateConnectionAsync();
                await conn.OpenAsync();

                // 1. Load Game Cache Entries
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT GameId, Version, PackageId, TotalBlocks, CompletedBlocks, TotalSize, IsHealthy, LastUsedUtc FROM GameCacheEntries;";
                    using var reader = await cmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        var entry = new GameCacheEntry
                        {
                            GameId = reader.GetString(0),
                            Version = reader.GetString(1),
                            PackageId = reader.GetString(2),
                            TotalBlocks = reader.GetInt32(3),
                            CompletedBlocks = reader.GetInt32(4),
                            TotalSize = reader.GetInt64(5),
                            IsHealthy = reader.GetInt32(6) == 1,
                            LastUsedUtc = DateTime.Parse(reader.GetString(7))
                        };
                        _gameEntries[entry.GameId] = entry;
                    }
                }

                // 2. Load Cache Nodes
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT NodeId, MachineId, Hostname, IpAddress, Port, IsOnline, LastSeenUtc, FreeStorageBytes, IsSsd, NetworkSpeedMbps, CpuLoadPercent, CacheCompletenessPercent, HealthScore FROM CacheNodes;";
                    using var reader = await cmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        var node = new CacheNode
                        {
                            NodeId = reader.GetString(0),
                            MachineId = reader.GetString(1),
                            Hostname = reader.GetString(2),
                            IpAddress = reader.GetString(3),
                            Port = reader.GetInt32(4),
                            IsOnline = reader.GetInt32(5) == 1,
                            LastSeenUtc = DateTime.Parse(reader.GetString(6)),
                            FreeStorageBytes = reader.GetInt64(7),
                            IsSsd = reader.GetInt32(8) == 1,
                            NetworkSpeedMbps = reader.GetDouble(9),
                            CpuLoadPercent = reader.GetDouble(10),
                            CacheCompletenessPercent = reader.GetDouble(11),
                            HealthScore = reader.GetDouble(12)
                        };
                        _nodes[node.NodeId] = node;
                    }
                }

                // 3. Load Block Availabilities
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT NodeId, BlockId, GameId, IsAvailable FROM BlockAvailabilities;";
                    using var reader = await cmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        var avail = new BlockAvailability
                        {
                            NodeId = reader.GetString(0),
                            BlockId = reader.GetString(1),
                            GameId = reader.GetString(2),
                            IsAvailable = reader.GetInt32(3) == 1
                        };
                        var nodeDict = _blockAvailability.GetOrAdd(avail.BlockId, _ => new ConcurrentDictionary<string, BlockAvailability>());
                        nodeDict[avail.NodeId] = avail;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to load distributed cache data from SQLCipher DB. Fallback to in-memory mode.");
            }
        }

        public async Task SaveGameEntryAsync(GameCacheEntry entry, CancellationToken cancellationToken = default)
        {
            if (entry == null) throw new ArgumentNullException(nameof(entry));
            _gameEntries[entry.GameId] = entry;

            if (_dbContext != null)
            {
                try
                {
                    using var conn = await _dbContext.CreateConnectionAsync(cancellationToken);
                    await conn.OpenAsync(cancellationToken);

                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = @"
                        INSERT INTO GameCacheEntries (GameId, Version, PackageId, TotalBlocks, CompletedBlocks, TotalSize, IsHealthy, LastUsedUtc)
                        VALUES ($gId, $ver, $pId, $totB, $compB, $totS, $isH, $lastU)
                        ON CONFLICT(GameId) DO UPDATE SET
                            Version = excluded.Version,
                            PackageId = excluded.PackageId,
                            TotalBlocks = excluded.TotalBlocks,
                            CompletedBlocks = excluded.CompletedBlocks,
                            TotalSize = excluded.TotalSize,
                            IsHealthy = excluded.IsHealthy,
                            LastUsedUtc = excluded.LastUsedUtc;";

                    AddParam(cmd, "$gId", entry.GameId);
                    AddParam(cmd, "$ver", entry.Version);
                    AddParam(cmd, "$pId", entry.PackageId);
                    AddParam(cmd, "$totB", entry.TotalBlocks);
                    AddParam(cmd, "$compB", entry.CompletedBlocks);
                    AddParam(cmd, "$totS", entry.TotalSize);
                    AddParam(cmd, "$isH", entry.IsHealthy ? 1 : 0);
                    AddParam(cmd, "$lastU", entry.LastUsedUtc.ToString("O"));

                    await cmd.ExecuteNonQueryAsync(cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Failed to save GameCacheEntry '{GameId}' to SQLCipher DB.", entry.GameId);
                }
            }
        }

        public Task<GameCacheEntry?> GetGameEntryAsync(string gameId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(gameId)) return Task.FromResult<GameCacheEntry?>(null);
            _gameEntries.TryGetValue(gameId, out var entry);
            return Task.FromResult(entry);
        }

        public Task<IEnumerable<GameCacheEntry>> GetAllGameEntriesAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IEnumerable<GameCacheEntry>>(_gameEntries.Values.ToList());
        }

        public async Task SaveNodeAsync(CacheNode node, CancellationToken cancellationToken = default)
        {
            if (node == null) throw new ArgumentNullException(nameof(node));
            _nodes[node.NodeId] = node;

            if (_dbContext != null)
            {
                try
                {
                    using var conn = await _dbContext.CreateConnectionAsync(cancellationToken);
                    await conn.OpenAsync(cancellationToken);

                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = @"
                        INSERT INTO CacheNodes (NodeId, MachineId, Hostname, IpAddress, Port, IsOnline, LastSeenUtc, FreeStorageBytes, IsSsd, NetworkSpeedMbps, CpuLoadPercent, CacheCompletenessPercent, HealthScore)
                        VALUES ($nId, $mId, $host, $ip, $port, $isO, $lastS, $freeS, $isSsd, $speed, $cpu, $comp, $health)
                        ON CONFLICT(NodeId) DO UPDATE SET
                            MachineId = excluded.MachineId,
                            Hostname = excluded.Hostname,
                            IpAddress = excluded.IpAddress,
                            Port = excluded.Port,
                            IsOnline = excluded.IsOnline,
                            LastSeenUtc = excluded.LastSeenUtc,
                            FreeStorageBytes = excluded.FreeStorageBytes,
                            IsSsd = excluded.IsSsd,
                            NetworkSpeedMbps = excluded.NetworkSpeedMbps,
                            CpuLoadPercent = excluded.CpuLoadPercent,
                            CacheCompletenessPercent = excluded.CacheCompletenessPercent,
                            HealthScore = excluded.HealthScore;";

                    AddParam(cmd, "$nId", node.NodeId);
                    AddParam(cmd, "$mId", node.MachineId);
                    AddParam(cmd, "$host", node.Hostname);
                    AddParam(cmd, "$ip", node.IpAddress);
                    AddParam(cmd, "$port", node.Port);
                    AddParam(cmd, "$isO", node.IsOnline ? 1 : 0);
                    AddParam(cmd, "$lastS", node.LastSeenUtc.ToString("O"));
                    AddParam(cmd, "$freeS", node.FreeStorageBytes);
                    AddParam(cmd, "$isSsd", node.IsSsd ? 1 : 0);
                    AddParam(cmd, "$speed", node.NetworkSpeedMbps);
                    AddParam(cmd, "$cpu", node.CpuLoadPercent);
                    AddParam(cmd, "$comp", node.CacheCompletenessPercent);
                    AddParam(cmd, "$health", node.HealthScore);

                    await cmd.ExecuteNonQueryAsync(cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Failed to save CacheNode '{NodeId}' to SQLCipher DB.", node.NodeId);
                }
            }
        }

        public Task<CacheNode?> GetNodeAsync(string nodeId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(nodeId)) return Task.FromResult<CacheNode?>(null);
            _nodes.TryGetValue(nodeId, out var node);
            return Task.FromResult(node);
        }

        public Task<IEnumerable<CacheNode>> GetOnlineNodesAsync(CancellationToken cancellationToken = default)
        {
            var online = _nodes.Values.Where(n => n.IsOnline && (DateTime.UtcNow - n.LastSeenUtc).TotalSeconds < 30).ToList();
            return Task.FromResult<IEnumerable<CacheNode>>(online);
        }

        public async Task SaveBlockAvailabilityAsync(BlockAvailability availability, CancellationToken cancellationToken = default)
        {
            if (availability == null) throw new ArgumentNullException(nameof(availability));

            var nodeDict = _blockAvailability.GetOrAdd(availability.BlockId, _ => new ConcurrentDictionary<string, BlockAvailability>());
            nodeDict[availability.NodeId] = availability;

            if (_dbContext != null)
            {
                try
                {
                    using var conn = await _dbContext.CreateConnectionAsync(cancellationToken);
                    await conn.OpenAsync(cancellationToken);

                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = @"
                        INSERT INTO BlockAvailabilities (NodeId, BlockId, GameId, IsAvailable)
                        VALUES ($nId, $bId, $gId, $isA)
                        ON CONFLICT(NodeId, BlockId) DO UPDATE SET
                            GameId = excluded.GameId,
                            IsAvailable = excluded.IsAvailable;";

                    AddParam(cmd, "$nId", availability.NodeId);
                    AddParam(cmd, "$bId", availability.BlockId);
                    AddParam(cmd, "$gId", availability.GameId);
                    AddParam(cmd, "$isA", availability.IsAvailable ? 1 : 0);

                    await cmd.ExecuteNonQueryAsync(cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Failed to save BlockAvailability for node '{NodeId}' block '{BlockId}' to SQLCipher DB.",
                        availability.NodeId, availability.BlockId);
                }
            }
        }

        public Task<IEnumerable<CacheNode>> GetNodesWithBlockAsync(string blockId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(blockId)) return Task.FromResult<IEnumerable<CacheNode>>(Enumerable.Empty<CacheNode>());

            var list = new List<CacheNode>();
            if (_blockAvailability.TryGetValue(blockId, out var nodeDict))
            {
                foreach (var kvp in nodeDict)
                {
                    if (kvp.Value.IsAvailable && _nodes.TryGetValue(kvp.Key, out var node))
                    {
                        if (node.IsOnline && (DateTime.UtcNow - node.LastSeenUtc).TotalSeconds < 30)
                        {
                            list.Add(node);
                        }
                    }
                }
            }

            return Task.FromResult<IEnumerable<CacheNode>>(list);
        }

        public Task<IEnumerable<string>> GetAvailableBlocksForNodeAsync(string nodeId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(nodeId)) return Task.FromResult<IEnumerable<string>>(Enumerable.Empty<string>());

            var availableBlocks = new List<string>();
            foreach (var kvp in _blockAvailability)
            {
                if (kvp.Value.TryGetValue(nodeId, out var blockAvail) && blockAvail.IsAvailable)
                {
                    availableBlocks.Add(kvp.Key);
                }
            }

            return Task.FromResult<IEnumerable<string>>(availableBlocks);
        }

        private void AddParam(DbCommand cmd, string name, object value)
        {
            var p = cmd.CreateParameter();
            p.ParameterName = name;
            p.Value = value ?? DBNull.Value;
            cmd.Parameters.Add(p);
        }
    }
}
