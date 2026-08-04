using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sayra.Client.Shared.Fleet.Assets.Interfaces;
using Sayra.Client.Shared.Interfaces.Phase9;
using Sayra.Client.Shared.Models.Phase9.Domain;
using Sayra.Client.Shared.Models.Phase9.Enums;

namespace Sayra.Client.Shared.Fleet.Assets.Services
{
    /// <summary>
    /// Service coordinating initial/incremental asset discovery, change detection, duplicate resolution, and history tracking.
    /// </summary>
    public class AssetDiscoveryEngine : IInventoryCollector
    {
        private readonly ILogger<AssetDiscoveryEngine> _logger;
        private readonly IAssetRepository _assetRepository;
        private readonly IEnumerable<IAssetCollector> _collectors;

        /// <summary>
        /// Initializes a new instance of the <see cref="AssetDiscoveryEngine"/> class.
        /// </summary>
        public AssetDiscoveryEngine(
            ILogger<AssetDiscoveryEngine> logger,
            IAssetRepository assetRepository,
            IEnumerable<IAssetCollector> collectors)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _assetRepository = assetRepository ?? throw new ArgumentNullException(nameof(assetRepository));
            _collectors = collectors ?? throw new ArgumentNullException(nameof(collectors));
        }

        /// <summary>
        /// Scans and returns local hardware/software inventory details.
        /// Implements <see cref="IInventoryCollector.CollectInventoryAsync"/>.
        /// </summary>
        public async Task<MachineInventory> CollectInventoryAsync(CancellationToken ct = default)
        {
            _logger.LogInformation("Scanning system for general machine inventory...");
            var assets = await DiscoverAssetsAsync("LocalMachine", isIncremental: false, ct: ct);

            // Populate MachineInventory from scanned assets
            var cpu = assets.FirstOrDefault(a => a.Category == AssetType.Cpu);
            var gpu = assets.FirstOrDefault(a => a.Category == AssetType.Gpu);
            var ram = assets.FirstOrDefault(a => a.Category == AssetType.Ram);
            var storage = assets.Where(a => a.Category == AssetType.StorageDevice).ToList();

            var storageDrives = new Dictionary<string, string>();
            foreach (var s in storage)
            {
                string deviceName = s.Specifications.GetValueOrDefault("DeviceName") ?? s.Name;
                string size = s.Specifications.GetValueOrDefault("SizeBytes") ?? "0";
                storageDrives[deviceName] = size;
            }

            int ramGb = 0;
            if (ram != null && ram.Specifications.TryGetValue("CapacityBytes", out var capStr) && long.TryParse(capStr, out var bytes))
            {
                ramGb = (int)(bytes / (1024 * 1024 * 1024));
            }

            return new MachineInventory
            {
                CpuName = cpu?.Name ?? "Unknown CPU",
                GpuName = gpu?.Name ?? "Unknown GPU",
                RamGb = ramGb > 0 ? ramGb : 16,
                OperatingSystem = RuntimeInformation.OSDescription,
                StorageDrives = storageDrives
            };
        }

        /// <summary>
        /// Runs asset discovery for a target machine.
        /// Supports Initial Discovery, Incremental Discovery, and full Change/Duplicate Detection.
        /// </summary>
        public async Task<IReadOnlyList<AssetRecord>> DiscoverAssetsAsync(string machineId, bool isIncremental = false, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(machineId)) throw new ArgumentException("Machine ID cannot be empty", nameof(machineId));

            _logger.LogInformation("Starting asset discovery for machine '{MachineId}' (Incremental: {Incremental})...", machineId, isIncremental);

            var newlyCollected = new List<AssetRecord>();

            // Run all independent collectors in parallel with safety
            var tasks = _collectors.Select(async collector =>
            {
                try
                {
                    var results = await collector.CollectAssetsAsync(machineId, ct);
                    if (results != null)
                    {
                        lock (newlyCollected)
                        {
                            newlyCollected.AddRange(results);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Collector '{Collector}' failed during asset scanning.", collector.GetType().Name);
                }
            });

            await Task.WhenAll(tasks);

            // Part 4: Duplicate Detection
            var uniqueAssets = DeduplicateAssets(newlyCollected);

            // Fetch previous assets from database to perform Change Detection
            var existingAssets = await _assetRepository.GetAssetsByMachineAsync(machineId, ct);

            if (existingAssets.Count == 0)
            {
                // Part 4: Initial Discovery
                _logger.LogInformation("No existing assets found. Performing Initial Discovery registration.");
                foreach (var asset in uniqueAssets)
                {
                    await _assetRepository.SaveAssetAsync(asset, ct);

                    // Track Asset History: First Seen
                    await _assetRepository.RecordHistoryAsync(new AssetHistory
                    {
                        HistoryId = Guid.NewGuid().ToString(),
                        AssetId = asset.AssetId,
                        MachineId = machineId,
                        TimestampUtc = DateTime.UtcNow,
                        EventType = "FirstSeen",
                        Description = $"Asset '{asset.Name}' was discovered for the first time.",
                        OperatorId = "System"
                    }, ct);
                }
            }
            else
            {
                // Part 4: Incremental Discovery & Change Detection
                _logger.LogInformation("Performing incremental change detection comparing {NewCount} new assets against {OldCount} old ones.", uniqueAssets.Count, existingAssets.Count);
                await PerformChangeDetectionAsync(machineId, uniqueAssets, existingAssets, isIncremental, ct);
            }

            // Sync database inventory snapshot
            await _assetRepository.SaveInventorySnapshotAsync(machineId, uniqueAssets, ct);

            return uniqueAssets;
        }

        private List<AssetRecord> DeduplicateAssets(List<AssetRecord> source)
        {
            var unique = new Dictionary<string, AssetRecord>();
            foreach (var asset in source)
            {
                // We resolve duplicate asset registrations based on SerialOrSignature key
                string key = string.IsNullOrEmpty(asset.SerialOrSignature) ? asset.AssetId : asset.SerialOrSignature;
                if (!unique.ContainsKey(key))
                {
                    unique[key] = asset;
                }
                else
                {
                    _logger.LogWarning("Duplicate asset detected and resolved: Name='{Name}', Serial='{Serial}'", asset.Name, asset.SerialOrSignature);
                }
            }
            return unique.Values.ToList();
        }

        private async Task PerformChangeDetectionAsync(
            string machineId,
            List<AssetRecord> newAssets,
            IReadOnlyList<AssetRecord> oldAssets,
            bool isIncremental,
            CancellationToken ct)
        {
            var oldMap = oldAssets.ToDictionary(a => a.AssetId);

            // Detect Added / Modified
            foreach (var newAsset in newAssets)
            {
                if (!oldMap.TryGetValue(newAsset.AssetId, out var oldAsset))
                {
                    // Asset Added (First Seen)
                    await _assetRepository.SaveAssetAsync(newAsset, ct);

                    string eventType = newAsset.Category == AssetType.Software ? "SoftwareChanges" : "HardwareReplacement";
                    await _assetRepository.RecordHistoryAsync(new AssetHistory
                    {
                        HistoryId = Guid.NewGuid().ToString(),
                        AssetId = newAsset.AssetId,
                        MachineId = machineId,
                        TimestampUtc = DateTime.UtcNow,
                        EventType = eventType,
                        Description = $"New asset '{newAsset.Name}' was newly added or replaced.",
                        OperatorId = "System"
                    }, ct);
                }
                else
                {
                    // Compare specifications for Changes and Version Changes
                    bool hasChanges = false;
                    foreach (var kvp in newAsset.Specifications)
                    {
                        if (!oldAsset.Specifications.TryGetValue(kvp.Key, out var oldVal) || oldVal != kvp.Value)
                        {
                            hasChanges = true;

                            // Record granular change record
                            string changeType = kvp.Key == "Version" || kvp.Key == "DriverVersion" ? "VersionChanges" : "Changes";
                            await _assetRepository.RecordChangeAsync(new AssetChangeRecord
                            {
                                ChangeId = Guid.NewGuid().ToString(),
                                AssetId = newAsset.AssetId,
                                MachineId = machineId,
                                TimestampUtc = DateTime.UtcNow,
                                ChangeType = changeType,
                                PropertyName = kvp.Key,
                                OldValue = oldVal ?? string.Empty,
                                NewValue = kvp.Value
                            }, ct);
                        }
                    }

                    if (hasChanges)
                    {
                        await _assetRepository.SaveAssetAsync(newAsset, ct);

                        await _assetRepository.RecordHistoryAsync(new AssetHistory
                        {
                            HistoryId = Guid.NewGuid().ToString(),
                            AssetId = newAsset.AssetId,
                            MachineId = machineId,
                            TimestampUtc = DateTime.UtcNow,
                            EventType = "Changes",
                            Description = $"Asset specifications for '{newAsset.Name}' were modified.",
                            OperatorId = "System"
                        }, ct);
                    }
                    else if (!isIncremental)
                    {
                        // Asset is unchanged, update Last Seen or simply record it
                        await _assetRepository.RecordHistoryAsync(new AssetHistory
                        {
                            HistoryId = Guid.NewGuid().ToString(),
                            AssetId = newAsset.AssetId,
                            MachineId = machineId,
                            TimestampUtc = DateTime.UtcNow,
                            EventType = "LastSeen",
                            Description = $"Asset '{newAsset.Name}' verified as unchanged.",
                            OperatorId = "System"
                        }, ct);
                    }
                }
            }

            // If not doing incremental-only, detect Removed assets
            if (!isIncremental)
            {
                var newIds = new HashSet<string>(newAssets.Select(a => a.AssetId));
                foreach (var oldAsset in oldAssets)
                {
                    if (!newIds.Contains(oldAsset.AssetId))
                    {
                        // Asset Removed
                        await _assetRepository.DeleteAssetAsync(oldAsset.AssetId, ct);

                        string eventType = oldAsset.Category == AssetType.Software ? "SoftwareChanges" : "HardwareReplacement";
                        await _assetRepository.RecordHistoryAsync(new AssetHistory
                        {
                            HistoryId = Guid.NewGuid().ToString(),
                            AssetId = oldAsset.AssetId,
                            MachineId = machineId,
                            TimestampUtc = DateTime.UtcNow,
                            EventType = eventType,
                            Description = $"Asset '{oldAsset.Name}' was uninstalled or removed from the system.",
                            OperatorId = "System"
                        }, ct);
                    }
                }
            }
        }
    }
}
