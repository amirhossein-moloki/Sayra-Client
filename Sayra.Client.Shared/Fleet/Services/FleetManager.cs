using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sayra.Client.Shared.Fleet.Interfaces;
using Sayra.Client.Shared.Interfaces.Phase9;
using Sayra.Client.Shared.Models.Phase9.Domain;
using Sayra.Client.Shared.Models.Phase9.Enums;
using Sayra.Client.Shared.Models.Phase9.Events;
using Sayra.Client.Shared.Interfaces;

namespace Sayra.Client.Shared.Fleet.Services
{
    /// <summary>
    /// Core coordinator orchestrating machine registration, group evaluation, and synchronization.
    /// Implements Phase 9 contract <see cref="IFleetManager"/>.
    /// </summary>
    public class FleetManager : Sayra.Client.Shared.Interfaces.Phase9.IFleetManager
    {
        private readonly IMachineRepository _machineRepo;
        private readonly Sayra.Client.Shared.Fleet.Interfaces.IGroupRepository _groupRepo;
        private readonly ITagRepository _tagRepo;
        private readonly IFleetCache _cache;
        private readonly IEventDispatcher _eventDispatcher;
        private readonly ILogger<FleetManager> _logger;

        private static readonly Regex IpRegex = new(@"^(\d{1,3}\.){3}\d{1,3}$", RegexOptions.Compiled);
        private static readonly Regex MacRegex = new(@"^([0-9A-Fa-f]{2}[:-]){5}([0-9A-Fa-f]{2})$", RegexOptions.Compiled);

        /// <summary>
        /// Initializes a new instance of the <see cref="FleetManager"/> class.
        /// </summary>
        public FleetManager(
            IMachineRepository machineRepo,
            Sayra.Client.Shared.Fleet.Interfaces.IGroupRepository groupRepo,
            ITagRepository tagRepo,
            IFleetCache cache,
            IEventDispatcher eventDispatcher,
            ILogger<FleetManager> logger)
        {
            _machineRepo = machineRepo ?? throw new ArgumentNullException(nameof(machineRepo));
            _groupRepo = groupRepo ?? throw new ArgumentNullException(nameof(groupRepo));
            _tagRepo = tagRepo ?? throw new ArgumentNullException(nameof(tagRepo));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _eventDispatcher = eventDispatcher ?? throw new ArgumentNullException(nameof(eventDispatcher));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public async Task<bool> RegisterMachineAsync(MachineInfo machine, CancellationToken ct = default)
        {
            if (machine == null) throw new ArgumentNullException(nameof(machine));

            _logger.LogInformation("Processing registration for machine '{MachineId}' (IP: '{Ip}')", machine.MachineId, machine.IpAddress);

            // 1. Structural Validation
            ValidateMachineIdentity(machine);

            // 2. Duplicate Detection
            var existing = await _machineRepo.GetAsync(machine.MachineId, ct);
            bool isNew = existing == null;

            if (isNew)
            {
                // Check MAC or IP duplicates among other machines
                var all = await _machineRepo.GetAllAsync(ct);
                foreach (var other in all)
                {
                    if (string.Equals(other.MacAddress, machine.MacAddress, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidOperationException($"Duplicate MAC address violation: Machine '{other.MachineId}' already owns MAC '{machine.MacAddress}'.");
                    }
                    if (string.Equals(other.IpAddress, machine.IpAddress, StringComparison.OrdinalIgnoreCase) && other.Status == MachineStatus.Online)
                    {
                        _logger.LogWarning("IP collision detected: Machine '{OtherId}' is active on '{IP}'. Overwriting.", other.MachineId, machine.IpAddress);
                    }
                }
            }

            // 3. Persistent Save & Cache Update
            bool saved = await _machineRepo.SaveAsync(machine, ct);
            if (saved)
            {
                _cache.SetMachine(machine);

                // 4. Dynamic Group memberships evaluation
                await EvaluateDynamicGroupsForMachineAsync(machine, ct);

                // 5. Dispatch domain events
                if (isNew)
                {
                    _eventDispatcher.Dispatch(new MachineRegistered(machine.MachineId, machine.Hostname, machine.IpAddress));
                }

                if (machine.Status == MachineStatus.Online)
                {
                    _eventDispatcher.Dispatch(new MachineOnline(machine.MachineId));
                    _eventDispatcher.Dispatch(new MachineConnected(machine.MachineId, machine.IpAddress));
                }
                else if (machine.Status == MachineStatus.Offline)
                {
                    _eventDispatcher.Dispatch(new MachineOffline(machine.MachineId));
                    _eventDispatcher.Dispatch(new MachineDisconnected(machine.MachineId));
                }
            }

            return saved;
        }

        /// <inheritdoc />
        public async Task<bool> RemoveMachineAsync(string machineId, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(machineId)) return false;

            _logger.LogInformation("Removing machine '{MachineId}' from the fleet registry", machineId);

            bool deleted = await _machineRepo.DeleteAsync(machineId, ct);
            if (deleted)
            {
                _cache.InvalidateMachine(machineId);
                _eventDispatcher.Dispatch(new MachineRemoved(machineId));
                _eventDispatcher.Dispatch(new MachineOffline(machineId));
                _eventDispatcher.Dispatch(new MachineDisconnected(machineId));
            }

            return deleted;
        }

        /// <inheritdoc />
        public async Task<MachineInfo?> GetMachineAsync(string machineId, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(machineId)) return null;

            // Query cache first
            var cached = _cache.GetMachine(machineId);
            if (cached != null) return cached;

            // Query repository
            var dbRecord = await _machineRepo.GetAsync(machineId, ct);
            if (dbRecord != null)
            {
                _cache.SetMachine(dbRecord);
            }

            return dbRecord;
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<MachineInfo>> GetAllMachinesAsync(CancellationToken ct = default)
        {
            var cached = _cache.GetAllMachines();
            if (cached.Count > 0) return cached;

            var dbRecords = await _machineRepo.GetAllAsync(ct);
            foreach (var record in dbRecords)
            {
                _cache.SetMachine(record);
            }

            return dbRecords;
        }

        /// <inheritdoc />
        public async Task<bool> CreateGroupAsync(FleetGroup group, CancellationToken ct = default)
        {
            if (group == null) throw new ArgumentNullException(nameof(group));

            _logger.LogInformation("Creating fleet group '{GroupId}' ({Name}, Type: {Type})", group.GroupId, group.Name, group.GroupType);

            // Validate expression syntax if dynamic
            if (group.GroupType == FleetGroupType.Dynamic && string.IsNullOrEmpty(group.DynamicRuleExpression))
            {
                throw new ArgumentException("Dynamic rule expression cannot be empty for dynamic groups.");
            }

            bool saved = await _groupRepo.SaveGroupAsync(group, ct);
            if (saved)
            {
                _cache.SetGroup(group);

                // Evaluate memberships for all workstations if group is Dynamic
                if (group.GroupType == FleetGroupType.Dynamic)
                {
                    await EvaluateDynamicGroupMembershipsAsync(group, ct);
                }

                _eventDispatcher.Dispatch(new FleetGroupCreated(group.GroupId, group.Name, group.GroupType));
            }

            return saved;
        }

        /// <inheritdoc />
        public async Task<bool> DeleteGroupAsync(string groupId, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(groupId)) return false;

            _logger.LogInformation("Deleting fleet group '{GroupId}'", groupId);

            bool deleted = await _groupRepo.DeleteGroupAsync(groupId, ct);
            if (deleted)
            {
                _cache.InvalidateGroup(groupId);
                _eventDispatcher.Dispatch(new FleetGroupDeleted(groupId));
            }

            return deleted;
        }

        /// <inheritdoc />
        public async Task<bool> AssignMachineToGroupAsync(string machineId, string groupId, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(machineId) || string.IsNullOrEmpty(groupId)) return false;

            var group = await _groupRepo.GetGroupAsync(groupId, ct);
            if (group == null)
            {
                throw new KeyNotFoundException($"Group '{groupId}' does not exist.");
            }

            if (group.GroupType == FleetGroupType.Dynamic)
            {
                throw new InvalidOperationException("Explicit membership assignments cannot be performed on dynamic groups.");
            }

            _logger.LogInformation("Assigning machine '{MachineId}' to static group '{GroupId}'", machineId, groupId);
            return await _groupRepo.AssignMachineAsync(machineId, groupId, ct);
        }

        /// <inheritdoc />
        public async Task<bool> RemoveMachineFromGroupAsync(string machineId, string groupId, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(machineId) || string.IsNullOrEmpty(groupId)) return false;

            var group = await _groupRepo.GetGroupAsync(groupId, ct);
            if (group != null && group.GroupType == FleetGroupType.Dynamic)
            {
                throw new InvalidOperationException("Explicit membership removals cannot be performed on dynamic groups.");
            }

            _logger.LogInformation("Removing machine '{MachineId}' from group '{GroupId}'", machineId, groupId);
            return await _groupRepo.RemoveMachineAsync(machineId, groupId, ct);
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<MachineInfo>> GetGroupMembersAsync(string groupId, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(groupId)) return Array.Empty<MachineInfo>();

            var group = await _groupRepo.GetGroupAsync(groupId, ct);
            if (group == null) return Array.Empty<MachineInfo>();

            // If group is dynamic, evaluate memberships to make sure they are fresh
            if (group.GroupType == FleetGroupType.Dynamic)
            {
                await EvaluateDynamicGroupMembershipsAsync(group, ct);
            }

            var ids = await _groupRepo.GetMachineIdsInGroupAsync(groupId, ct);
            var members = new List<MachineInfo>();
            foreach (var id in ids)
            {
                var machine = await GetMachineAsync(id, ct);
                if (machine != null)
                {
                    members.Add(machine);
                }
            }

            return members;
        }

        private void ValidateMachineIdentity(MachineInfo machine)
        {
            if (string.IsNullOrWhiteSpace(machine.MachineId))
            {
                throw new ArgumentException("MachineId cannot be empty.");
            }

            if (string.IsNullOrWhiteSpace(machine.Hostname))
            {
                throw new ArgumentException("Hostname cannot be empty.");
            }

            // IP Address validation
            if (string.IsNullOrEmpty(machine.IpAddress) || !IpRegex.IsMatch(machine.IpAddress))
            {
                throw new ArgumentException($"Invalid IP Address format: '{machine.IpAddress}'.");
            }

            // MAC Address validation
            if (string.IsNullOrEmpty(machine.MacAddress) || !MacRegex.IsMatch(machine.MacAddress))
            {
                throw new ArgumentException($"Invalid MAC Address format: '{machine.MacAddress}'.");
            }
        }

        private async Task EvaluateDynamicGroupsForMachineAsync(MachineInfo machine, CancellationToken ct)
        {
            var groups = await _groupRepo.GetAllGroupsAsync(ct);
            var tags = await _tagRepo.GetTagsForMachineAsync(machine.MachineId, ct);

            foreach (var group in groups)
            {
                if (group.GroupType == FleetGroupType.Dynamic)
                {
                    bool isMember = DynamicRuleEvaluator.Evaluate(group.DynamicRuleExpression, machine, tags);
                    if (isMember)
                    {
                        await _groupRepo.AssignMachineAsync(machine.MachineId, group.GroupId, ct);
                    }
                    else
                    {
                        await _groupRepo.RemoveMachineAsync(machine.MachineId, group.GroupId, ct);
                    }
                }
            }
        }

        private async Task EvaluateDynamicGroupMembershipsAsync(FleetGroup group, CancellationToken ct)
        {
            var machines = await _machineRepo.GetAllAsync(ct);
            var matchingIds = new List<string>();

            foreach (var machine in machines)
            {
                var tags = await _tagRepo.GetTagsForMachineAsync(machine.MachineId, ct);
                bool isMember = DynamicRuleEvaluator.Evaluate(group.DynamicRuleExpression, machine, tags);
                if (isMember)
                {
                    matchingIds.Add(machine.MachineId);
                }
            }

            await _groupRepo.SyncGroupMembershipsAsync(group.GroupId, matchingIds, ct);
        }
    }
}
