using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sayra.Client.Shared.Fleet.Interfaces;
using Sayra.Client.Shared.Interfaces.Phase9;
using Sayra.Client.Shared.Models.Phase9.Domain;
using Sayra.Client.Shared.Models.Phase9.Enums;

namespace Sayra.Client.Shared.Fleet.BulkOperations
{
    /// <summary>
    /// Contract for resolving workstation targeting criteria into flat, de-duplicated lists of target machine records.
    /// </summary>
    public interface ITargetResolver
    {
        /// <summary>
        /// Resolves a set of bulk operation targets into unique, validated target machine information.
        /// </summary>
        /// <param name="targets">The collection of target criteria.</param>
        /// <param name="requiredCapabilities">Optional list of required features/capabilities to check on resolved targets.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>A collection of de-duplicated, validated machine records matching the criteria.</returns>
        Task<IReadOnlyList<MachineInfo>> ResolveTargetsAsync(
            IEnumerable<BulkOperationTarget> targets,
            IEnumerable<string>? requiredCapabilities = null,
            CancellationToken ct = default);

        /// <summary>
        /// Checks whether a single resolved machine is currently considered offline.
        /// </summary>
        bool IsOffline(MachineInfo machine);

        /// <summary>
        /// Verifies whether a machine meets a specific set of capability requirements.
        /// </summary>
        bool MeetsCapabilities(MachineInfo machine, IEnumerable<string> capabilities);
    }

    /// <summary>
    /// Thread-safe Target Resolver implementing validation, duplicate detection, offline detection, and capability checks.
    /// </summary>
    public class TargetResolver : ITargetResolver
    {
        private readonly IFleetManager _fleetManager;
        private readonly ITagManager _tagManager;
        private readonly ILogger<TargetResolver> _logger;

        /// <summary>
        /// Initializes a new instance of TargetResolver.
        /// </summary>
        public TargetResolver(
            IFleetManager fleetManager,
            ITagManager tagManager,
            ILogger<TargetResolver> logger)
        {
            _fleetManager = fleetManager ?? throw new ArgumentNullException(nameof(fleetManager));
            _tagManager = tagManager ?? throw new ArgumentNullException(nameof(tagManager));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<MachineInfo>> ResolveTargetsAsync(
            IEnumerable<BulkOperationTarget> targets,
            IEnumerable<string>? requiredCapabilities = null,
            CancellationToken ct = default)
        {
            if (targets == null) throw new ArgumentNullException(nameof(targets));

            var resolvedMachines = new Dictionary<string, MachineInfo>(StringComparer.OrdinalIgnoreCase);

            foreach (var target in targets)
            {
                if (target == null || !target.Validate())
                {
                    _logger.LogWarning("Skipping invalid bulk operation target criteria.");
                    continue;
                }

                _logger.LogInformation("Resolving target: Type={Type}, Value={Value}", target.TargetType, target.TargetValue);

                switch (target.TargetType)
                {
                    case BulkTargetType.Individual:
                        var machine = await _fleetManager.GetMachineAsync(target.TargetValue, ct);
                        if (machine != null)
                        {
                            resolvedMachines[machine.MachineId] = machine;
                        }
                        else
                        {
                            _logger.LogWarning("Individual machine target '{Id}' was not found in active fleet registration.", target.TargetValue);
                        }
                        break;

                    case BulkTargetType.StaticGroup:
                    case BulkTargetType.DynamicGroup:
                        // Query the group members directly via the FleetManager
                        var groupMembers = await _fleetManager.GetGroupMembersAsync(target.TargetValue, ct);
                        foreach (var m in groupMembers)
                        {
                            resolvedMachines[m.MachineId] = m;
                        }
                        break;

                    case BulkTargetType.Region:
                        var allFleetForRegion = await _fleetManager.GetAllMachinesAsync(ct);
                        // Filter machines by checking custom 'Region' tag or hostname structure
                        foreach (var m in allFleetForRegion)
                        {
                            var tags = await _tagManager.GetTagsForMachineAsync(m.MachineId, ct);
                            var regionTag = tags.FirstOrDefault(t => t.Key.Equals("Region", StringComparison.OrdinalIgnoreCase));
                            if ((regionTag != null && regionTag.Value.Equals(target.TargetValue, StringComparison.OrdinalIgnoreCase)) ||
                                m.Hostname.StartsWith(target.TargetValue + "-", StringComparison.OrdinalIgnoreCase))
                            {
                                resolvedMachines[m.MachineId] = m;
                            }
                        }
                        break;

                    case BulkTargetType.GamingCenter:
                        var allFleetForCenter = await _fleetManager.GetAllMachinesAsync(ct);
                        // Filter machines where custom 'Center' or 'GamingCenter' tag matches
                        foreach (var m in allFleetForCenter)
                        {
                            var tags = await _tagManager.GetTagsForMachineAsync(m.MachineId, ct);
                            var centerTag = tags.FirstOrDefault(t => t.Key.Equals("Center", StringComparison.OrdinalIgnoreCase) || t.Key.Equals("GamingCenter", StringComparison.OrdinalIgnoreCase));
                            if (centerTag != null && centerTag.Value.Equals(target.TargetValue, StringComparison.OrdinalIgnoreCase))
                            {
                                resolvedMachines[m.MachineId] = m;
                            }
                        }
                        break;

                    case BulkTargetType.Tag:
                        // Parse target value format "Key=Value"
                        var parts = target.TargetValue.Split('=', 2);
                        if (parts.Length == 2)
                        {
                            var key = parts[0].Trim();
                            var val = parts[1].Trim();
                            var machineIdsWithTag = await _tagManager.SearchMachinesByTagAsync(key, val, ct);
                            foreach (var id in machineIdsWithTag)
                            {
                                var m = await _fleetManager.GetMachineAsync(id, ct);
                                if (m != null)
                                {
                                    resolvedMachines[m.MachineId] = m;
                                }
                            }
                        }
                        else
                        {
                            _logger.LogWarning("Invalid tag filter value: '{Val}'. Expected format 'Key=Value'.", target.TargetValue);
                        }
                        break;

                    case BulkTargetType.HealthGroup:
                        if (Enum.TryParse<MachineHealthStatus>(target.TargetValue, true, out var targetHealth))
                        {
                            var allFleetForHealth = await _fleetManager.GetAllMachinesAsync(ct);
                            foreach (var m in allFleetForHealth)
                            {
                                if (m.HealthStatus == targetHealth)
                                {
                                    resolvedMachines[m.MachineId] = m;
                                }
                            }
                        }
                        else
                        {
                            _logger.LogWarning("Invalid HealthGroup status target value: '{Val}'", target.TargetValue);
                        }
                        break;

                    default:
                        _logger.LogError("TargetType '{Type}' is currently unsupported.", target.TargetType);
                        break;
                }
            }

            // Perform Capability Verification & Filtering
            var finalList = new List<MachineInfo>();
            foreach (var m in resolvedMachines.Values)
            {
                if (requiredCapabilities != null && requiredCapabilities.Any())
                {
                    if (!MeetsCapabilities(m, requiredCapabilities))
                    {
                        _logger.LogInformation("Skipping machine '{MachineId}' as it fails required capability checks.", m.MachineId);
                        continue;
                    }
                }
                finalList.Add(m);
            }

            _logger.LogInformation("Target Resolution finished. Resolved {Count} unique workstations.", finalList.Count);
            return finalList;
        }

        /// <inheritdoc />
        public bool IsOffline(MachineInfo machine)
        {
            if (machine == null) return true;
            // Online/InSession/Locked/Maintenance/Transitioning statuses are active statuses, Offline is offline.
            // Also perform heartbeat decay checks: if last seen was > 60 seconds ago, consider offline.
            if (machine.Status == MachineStatus.Offline) return true;
            if (DateTime.UtcNow.Subtract(machine.LastSeenUtc).TotalSeconds > 60) return true;
            return false;
        }

        /// <inheritdoc />
        public bool MeetsCapabilities(MachineInfo machine, IEnumerable<string> capabilities)
        {
            if (machine == null) return false;
            if (capabilities == null) return true;

            foreach (var cap in capabilities)
            {
                var cleanCap = cap.Trim();
                if (string.IsNullOrEmpty(cleanCap)) continue;

                // Support specialized semantic keyword checks:
                // "MinRam:16" -> requires RAM >= 16GB
                if (cleanCap.StartsWith("MinRam:", StringComparison.OrdinalIgnoreCase))
                {
                    if (int.TryParse(cleanCap.Substring(7), out var minRam))
                    {
                        if (machine.Inventory.RamGb < minRam) return false;
                    }
                }
                // "GPU:RTX" -> requires RTX cards
                else if (cleanCap.StartsWith("GPU:", StringComparison.OrdinalIgnoreCase))
                {
                    var expectedGpu = cleanCap.Substring(4);
                    if (!machine.Inventory.GpuName.Contains(expectedGpu, StringComparison.OrdinalIgnoreCase)) return false;
                }
                // "OS:Windows11" -> requires Windows 11
                else if (cleanCap.StartsWith("OS:", StringComparison.OrdinalIgnoreCase))
                {
                    var expectedOs = cleanCap.Substring(3);
                    if (!machine.Inventory.OperatingSystem.Contains(expectedOs, StringComparison.OrdinalIgnoreCase)) return false;
                }
                // Generic tag checks (e.g. checks if custom properties / tags contain the capability)
                else
                {
                    // Fallback to checking GpuName, CpuName or OperatingSystem for presence of the capability string
                    bool hasMatch = machine.Inventory.GpuName.Contains(cleanCap, StringComparison.OrdinalIgnoreCase) ||
                                   machine.Inventory.CpuName.Contains(cleanCap, StringComparison.OrdinalIgnoreCase) ||
                                   machine.Inventory.OperatingSystem.Contains(cleanCap, StringComparison.OrdinalIgnoreCase);
                    if (!hasMatch) return false;
                }
            }

            return true;
        }
    }
}
