using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sayra.Client.Shared.Fleet.Interfaces;
using Sayra.Client.Shared.Models.Phase9.Domain;

namespace Sayra.Client.Shared.Fleet.Services
{
    /// <summary>
    /// High-performance thread-safe in-memory cache implementation of <see cref="IFleetCache"/>.
    /// </summary>
    public class FleetCache : IFleetCache, IDisposable
    {
        private readonly IMachineRepository _machineRepo;
        private readonly IGroupRepository _groupRepo;
        private readonly ISnapshotRepository _snapshotRepo;
        private readonly IHealthRepository _healthRepo;
        private readonly IInventoryRepository _inventoryRepo;
        private readonly Microsoft.Extensions.Logging.ILogger<FleetCache> _logger;

        private readonly ConcurrentDictionary<string, MachineInfo> _machines = new(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<string, FleetGroup> _groups = new(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<string, MachineSnapshot> _snapshots = new(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<string, MachineHealth> _healths = new(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<string, MachineInventory> _inventories = new(StringComparer.OrdinalIgnoreCase);

        private readonly ReaderWriterLockSlim _cacheLock = new(LockRecursionPolicy.NoRecursion);
        private bool _isDisposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="FleetCache"/> class.
        /// </summary>
        public FleetCache(
            IMachineRepository machineRepo,
            IGroupRepository groupRepo,
            ISnapshotRepository snapshotRepo,
            IHealthRepository healthRepo,
            IInventoryRepository inventoryRepo,
            Microsoft.Extensions.Logging.ILogger<FleetCache> logger)
        {
            _machineRepo = machineRepo ?? throw new ArgumentNullException(nameof(machineRepo));
            _groupRepo = groupRepo ?? throw new ArgumentNullException(nameof(groupRepo));
            _snapshotRepo = snapshotRepo ?? throw new ArgumentNullException(nameof(snapshotRepo));
            _healthRepo = healthRepo ?? throw new ArgumentNullException(nameof(healthRepo));
            _inventoryRepo = inventoryRepo ?? throw new ArgumentNullException(nameof(inventoryRepo));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public void SetMachine(MachineInfo machine)
        {
            if (machine == null) return;
            _cacheLock.EnterWriteLock();
            try
            {
                _machines[machine.MachineId] = machine;
                if (machine.Inventory != null)
                {
                    _inventories[machine.MachineId] = machine.Inventory;
                }
            }
            finally
            {
                _cacheLock.ExitWriteLock();
            }
        }

        /// <inheritdoc />
        public MachineInfo? GetMachine(string machineId)
        {
            if (string.IsNullOrEmpty(machineId)) return null;
            _cacheLock.EnterReadLock();
            try
            {
                return _machines.TryGetValue(machineId, out var machine) ? machine : null;
            }
            finally
            {
                _cacheLock.ExitReadLock();
            }
        }

        /// <inheritdoc />
        public IReadOnlyList<MachineInfo> GetAllMachines()
        {
            _cacheLock.EnterReadLock();
            try
            {
                var list = new List<MachineInfo>(_machines.Values);
                return list.AsReadOnly();
            }
            finally
            {
                _cacheLock.ExitReadLock();
            }
        }

        /// <inheritdoc />
        public void InvalidateMachine(string machineId)
        {
            if (string.IsNullOrEmpty(machineId)) return;
            _cacheLock.EnterWriteLock();
            try
            {
                _machines.TryRemove(machineId, out _);
                _inventories.TryRemove(machineId, out _);
                _snapshots.TryRemove(machineId, out _);
                _healths.TryRemove(machineId, out _);
            }
            finally
            {
                _cacheLock.ExitWriteLock();
            }
        }

        /// <inheritdoc />
        public void SetGroup(FleetGroup group)
        {
            if (group == null) return;
            _cacheLock.EnterWriteLock();
            try
            {
                _groups[group.GroupId] = group;
            }
            finally
            {
                _cacheLock.ExitWriteLock();
            }
        }

        /// <inheritdoc />
        public FleetGroup? GetGroup(string groupId)
        {
            if (string.IsNullOrEmpty(groupId)) return null;
            _cacheLock.EnterReadLock();
            try
            {
                return _groups.TryGetValue(groupId, out var group) ? group : null;
            }
            finally
            {
                _cacheLock.ExitReadLock();
            }
        }

        /// <inheritdoc />
        public IReadOnlyList<FleetGroup> GetAllGroups()
        {
            _cacheLock.EnterReadLock();
            try
            {
                var list = new List<FleetGroup>(_groups.Values);
                return list.AsReadOnly();
            }
            finally
            {
                _cacheLock.ExitReadLock();
            }
        }

        /// <inheritdoc />
        public void InvalidateGroup(string groupId)
        {
            if (string.IsNullOrEmpty(groupId)) return;
            _cacheLock.EnterWriteLock();
            try
            {
                _groups.TryRemove(groupId, out _);
            }
            finally
            {
                _cacheLock.ExitWriteLock();
            }
        }

        /// <inheritdoc />
        public void SetSnapshot(string machineId, MachineSnapshot snapshot)
        {
            if (string.IsNullOrEmpty(machineId) || snapshot == null) return;
            _cacheLock.EnterWriteLock();
            try
            {
                _snapshots[machineId] = snapshot;
            }
            finally
            {
                _cacheLock.ExitWriteLock();
            }
        }

        /// <inheritdoc />
        public MachineSnapshot? GetSnapshot(string machineId)
        {
            if (string.IsNullOrEmpty(machineId)) return null;
            _cacheLock.EnterReadLock();
            try
            {
                return _snapshots.TryGetValue(machineId, out var snapshot) ? snapshot : null;
            }
            finally
            {
                _cacheLock.ExitReadLock();
            }
        }

        /// <inheritdoc />
        public void InvalidateSnapshot(string machineId)
        {
            if (string.IsNullOrEmpty(machineId)) return;
            _cacheLock.EnterWriteLock();
            try
            {
                _snapshots.TryRemove(machineId, out _);
            }
            finally
            {
                _cacheLock.ExitWriteLock();
            }
        }

        /// <inheritdoc />
        public void SetHealth(string machineId, MachineHealth health)
        {
            if (string.IsNullOrEmpty(machineId) || health == null) return;
            _cacheLock.EnterWriteLock();
            try
            {
                _healths[machineId] = health;
            }
            finally
            {
                _cacheLock.ExitWriteLock();
            }
        }

        /// <inheritdoc />
        public MachineHealth? GetHealth(string machineId)
        {
            if (string.IsNullOrEmpty(machineId)) return null;
            _cacheLock.EnterReadLock();
            try
            {
                return _healths.TryGetValue(machineId, out var health) ? health : null;
            }
            finally
            {
                _cacheLock.ExitReadLock();
            }
        }

        /// <inheritdoc />
        public void InvalidateHealth(string machineId)
        {
            if (string.IsNullOrEmpty(machineId)) return;
            _cacheLock.EnterWriteLock();
            try
            {
                _healths.TryRemove(machineId, out _);
            }
            finally
            {
                _cacheLock.ExitWriteLock();
            }
        }

        /// <inheritdoc />
        public void SetInventory(string machineId, MachineInventory inventory)
        {
            if (string.IsNullOrEmpty(machineId) || inventory == null) return;
            _cacheLock.EnterWriteLock();
            try
            {
                _inventories[machineId] = inventory;
            }
            finally
            {
                _cacheLock.ExitWriteLock();
            }
        }

        /// <inheritdoc />
        public MachineInventory? GetInventory(string machineId)
        {
            if (string.IsNullOrEmpty(machineId)) return null;
            _cacheLock.EnterReadLock();
            try
            {
                return _inventories.TryGetValue(machineId, out var inventory) ? inventory : null;
            }
            finally
            {
                _cacheLock.ExitReadLock();
            }
        }

        /// <inheritdoc />
        public void InvalidateInventory(string machineId)
        {
            if (string.IsNullOrEmpty(machineId)) return;
            _cacheLock.EnterWriteLock();
            try
            {
                _inventories.TryRemove(machineId, out _);
            }
            finally
            {
                _cacheLock.ExitWriteLock();
            }
        }

        /// <inheritdoc />
        public async Task RefreshAllAsync(CancellationToken ct = default)
        {
            _logger.LogInformation("Refreshing enterprise fleet caches from persistent database...");

            // Fetch from repositories
            var machines = await _machineRepo.GetAllAsync(ct);
            var groups = await _groupRepo.GetAllGroupsAsync(ct);

            _cacheLock.EnterWriteLock();
            try
            {
                _machines.Clear();
                _groups.Clear();
                _inventories.Clear();
                _snapshots.Clear();
                _healths.Clear();

                foreach (var machine in machines)
                {
                    _machines[machine.MachineId] = machine;
                    if (machine.Inventory != null)
                    {
                        _inventories[machine.MachineId] = machine.Inventory;
                    }

                    // Pre-warm snapshots
                    var snapshot = await _snapshotRepo.GetAsync(machine.MachineId, ct);
                    if (snapshot != null)
                    {
                        _snapshots[machine.MachineId] = snapshot;
                    }

                    // Pre-warm health
                    var health = await _healthRepo.GetHealthAsync(machine.MachineId, ct);
                    if (health != null)
                    {
                        _healths[machine.MachineId] = health;
                    }
                }

                foreach (var group in groups)
                {
                    _groups[group.GroupId] = group;
                }

                _logger.LogInformation("Enterprise fleet caches warmed up successfully. " +
                                       "Machines: {MacCount}, Groups: {GrpCount}", _machines.Count, _groups.Count);
            }
            finally
            {
                _cacheLock.ExitWriteLock();
            }
        }

        /// <inheritdoc />
        public void Clear()
        {
            _cacheLock.EnterWriteLock();
            try
            {
                _machines.Clear();
                _groups.Clear();
                _inventories.Clear();
                _snapshots.Clear();
                _healths.Clear();
            }
            finally
            {
                _cacheLock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Disposes internal synchronization variables.
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;
            _cacheLock.Dispose();
        }
    }
}
