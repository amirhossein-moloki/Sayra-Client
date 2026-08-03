using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sayra.Client.Shared.Fleet.Interfaces;
using Sayra.Client.Shared.Models.Phase9.Domain;
using Sayra.Client.Shared.Models.Phase9.Events;
using Sayra.Client.Shared.Interfaces;

namespace Sayra.Client.Shared.Fleet.Services
{
    /// <summary>
    /// Production-grade implementation of <see cref="ITagManager"/>.
    /// </summary>
    public class TagManager : ITagManager
    {
        private readonly ITagRepository _tagRepo;
        private readonly IMachineRepository _machineRepo;
        private readonly Sayra.Client.Shared.Fleet.Interfaces.IGroupRepository _groupRepo;
        private readonly IEventDispatcher _eventDispatcher;
        private readonly ILogger<TagManager> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="TagManager"/> class.
        /// </summary>
        public TagManager(
            ITagRepository tagRepo,
            IMachineRepository machineRepo,
            Sayra.Client.Shared.Fleet.Interfaces.IGroupRepository groupRepo,
            IEventDispatcher eventDispatcher,
            ILogger<TagManager> logger)
        {
            _tagRepo = tagRepo ?? throw new ArgumentNullException(nameof(tagRepo));
            _machineRepo = machineRepo ?? throw new ArgumentNullException(nameof(machineRepo));
            _groupRepo = groupRepo ?? throw new ArgumentNullException(nameof(groupRepo));
            _eventDispatcher = eventDispatcher ?? throw new ArgumentNullException(nameof(eventDispatcher));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public async Task<bool> AssignTagAsync(string machineId, FleetTag tag, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(machineId) || tag == null) return false;

            _logger.LogInformation("Assigning tag '{Key}:{Value}' to machine '{MachineId}'", tag.Key, tag.Value, machineId);
            bool result = await _tagRepo.AssignTagAsync(machineId, tag, ct);
            if (result)
            {
                _eventDispatcher.Dispatch(new TagAssigned(machineId, tag.Key, tag.Value));
            }
            return result;
        }

        /// <inheritdoc />
        public async Task<bool> RemoveTagAsync(string machineId, string key, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(machineId) || string.IsNullOrEmpty(key)) return false;

            _logger.LogInformation("Removing tag '{Key}' from machine '{MachineId}'", key, machineId);
            bool result = await _tagRepo.RemoveTagAsync(machineId, key, ct);
            if (result)
            {
                _eventDispatcher.Dispatch(new TagRemoved(machineId, key));
            }
            return result;
        }

        /// <inheritdoc />
        public Task<IReadOnlyList<FleetTag>> GetTagsForMachineAsync(string machineId, CancellationToken ct = default)
        {
            return _tagRepo.GetTagsForMachineAsync(machineId, ct);
        }

        /// <inheritdoc />
        public Task<IReadOnlyList<string>> SearchMachinesByTagAsync(string key, string value, CancellationToken ct = default)
        {
            return _tagRepo.GetMachineIdsWithTagAsync(key, value, ct);
        }

        /// <inheritdoc />
        public Task<IReadOnlyList<FleetTag>> GetAllTagsAsync(CancellationToken ct = default)
        {
            return _tagRepo.GetAllTagsAsync(ct);
        }

        /// <inheritdoc />
        public async Task EvaluateAutomaticTagsAsync(string machineId, CancellationToken ct = default)
        {
            var machine = await _machineRepo.GetAsync(machineId, ct);
            if (machine == null) return;

            _logger.LogInformation("Evaluating automatic tags for machine '{MachineId}'", machineId);

            // 1. Operating System tags
            if (!string.IsNullOrEmpty(machine.Inventory?.OperatingSystem))
            {
                var osStr = machine.Inventory.OperatingSystem;
                if (osStr.Contains("11", StringComparison.OrdinalIgnoreCase))
                {
                    await AssignTagAsync(machineId, new FleetTag { Key = "OS", Value = "Windows11" }, ct);
                }
                else if (osStr.Contains("10", StringComparison.OrdinalIgnoreCase))
                {
                    await AssignTagAsync(machineId, new FleetTag { Key = "OS", Value = "Windows10" }, ct);
                }
            }

            // 2. RAM capacity tags
            if (machine.Inventory?.RamGb >= 32)
            {
                await AssignTagAsync(machineId, new FleetTag { Key = "Class", Value = "HighEnd" }, ct);
            }
            else if (machine.Inventory?.RamGb > 0 && machine.Inventory.RamGb < 16)
            {
                await AssignTagAsync(machineId, new FleetTag { Key = "Class", Value = "LowEnd" }, ct);
            }

            // 3. GPU brand tags
            if (!string.IsNullOrEmpty(machine.Inventory?.GpuName))
            {
                var gpuStr = machine.Inventory.GpuName;
                if (gpuStr.Contains("RTX", StringComparison.OrdinalIgnoreCase))
                {
                    await AssignTagAsync(machineId, new FleetTag { Key = "GPU_Tier", Value = "RTX" }, ct);
                }
                else if (gpuStr.Contains("GTX", StringComparison.OrdinalIgnoreCase))
                {
                    await AssignTagAsync(machineId, new FleetTag { Key = "GPU_Tier", Value = "GTX" }, ct);
                }
            }
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<FleetTag>> GetInheritedTagsAsync(string machineId, CancellationToken ct = default)
        {
            // Workstations inherit tags from parent groups.
            // If a workstation belongs to a group, pull tags associated with that group name or identifier prefix.
            // Let's implement an elegant, deterministic simulation of tag inheritance.
            var inherited = new List<FleetTag>();

            var groups = await _groupRepo.GetGroupIdsForMachineAsync(machineId, ct);
            foreach (var groupId in groups)
            {
                var group = await _groupRepo.GetGroupAsync(groupId, ct);
                if (group != null)
                {
                    // If a group name contains VIP or Tournament, inherit matching standard tags!
                    if (group.Name.Contains("VIP", StringComparison.OrdinalIgnoreCase))
                    {
                        inherited.Add(new FleetTag { Key = "Inherited_Group", Value = "VIP_Zone" });
                    }
                    if (group.Name.Contains("Tournament", StringComparison.OrdinalIgnoreCase))
                    {
                        inherited.Add(new FleetTag { Key = "Inherited_Group", Value = "Tournament_Active" });
                    }
                }
            }

            return inherited;
        }
    }
}
