using System;
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
    /// Reconciles local and server states with Last-Write-Wins conflict resolution.
    /// </summary>
    public class FleetSynchronizationService : IFleetSynchronizationService
    {
        private readonly IMachineRepository _machineRepo;
        private readonly ISnapshotRepository _snapshotRepo;
        private readonly IInventoryRepository _inventoryRepo;
        private readonly IHealthRepository _healthRepo;
        private readonly IFleetCache _cache;
        private readonly IEventDispatcher _eventDispatcher;
        private readonly ILogger<FleetSynchronizationService> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="FleetSynchronizationService"/> class.
        /// </summary>
        public FleetSynchronizationService(
            IMachineRepository machineRepo,
            ISnapshotRepository snapshotRepo,
            IInventoryRepository inventoryRepo,
            IHealthRepository healthRepo,
            IFleetCache cache,
            IEventDispatcher eventDispatcher,
            ILogger<FleetSynchronizationService> logger)
        {
            _machineRepo = machineRepo ?? throw new ArgumentNullException(nameof(machineRepo));
            _snapshotRepo = snapshotRepo ?? throw new ArgumentNullException(nameof(snapshotRepo));
            _inventoryRepo = inventoryRepo ?? throw new ArgumentNullException(nameof(inventoryRepo));
            _healthRepo = healthRepo ?? throw new ArgumentNullException(nameof(healthRepo));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _eventDispatcher = eventDispatcher ?? throw new ArgumentNullException(nameof(eventDispatcher));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public async Task<bool> SynchronizeMachineStateAsync(MachineInfo localState, MachineInfo serverState, CancellationToken ct = default)
        {
            if (localState == null || serverState == null) return false;

            _logger.LogInformation("Synchronizing state for machine '{MachineId}'. Conflict resolution: Last-Write-Wins", localState.MachineId);

            // Reconcile via Last-Write-Wins based on LastSeenUtc
            if (serverState.LastSeenUtc > localState.LastSeenUtc)
            {
                _logger.LogWarning("Conflict detected! Server state is newer for machine '{MachineId}'. Applying server state.", localState.MachineId);
                await _machineRepo.SaveAsync(serverState, ct);
                _cache.SetMachine(serverState);

                // Publish version changed if there's version drift
                if (localState.Version?.SemVer != serverState.Version?.SemVer)
                {
                    _eventDispatcher.Dispatch(new VersionChanged(serverState.MachineId, serverState.Version!));
                }

                return true;
            }
            else
            {
                _logger.LogInformation("Local state is newer or equal for machine '{MachineId}'. Keeping local state.", localState.MachineId);
                await _machineRepo.SaveAsync(localState, ct);
                _cache.SetMachine(localState);
                return true;
            }
        }

        /// <inheritdoc />
        public async Task<bool> SynchronizeSnapshotAsync(string machineId, MachineSnapshot snapshot, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(machineId) || snapshot == null) return false;
            _logger.LogInformation("Synchronizing snapshot for machine '{MachineId}'", machineId);
            bool result = await _snapshotRepo.SaveAsync(snapshot, ct);
            if (result)
            {
                _cache.SetSnapshot(machineId, snapshot);
            }
            return result;
        }

        /// <inheritdoc />
        public async Task<bool> SynchronizeInventoryAsync(string machineId, MachineInventory inventory, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(machineId) || inventory == null) return false;
            _logger.LogInformation("Synchronizing inventory for machine '{MachineId}'", machineId);
            bool result = await _inventoryRepo.SaveAsync(machineId, inventory, ct);
            if (result)
            {
                _cache.SetInventory(machineId, inventory);
                _eventDispatcher.Dispatch(new InventoryUpdated(machineId, inventory));
            }
            return result;
        }

        /// <inheritdoc />
        public async Task<bool> SynchronizeHealthAsync(string machineId, MachineHealth health, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(machineId) || health == null) return false;
            _logger.LogInformation("Synchronizing health scores for machine '{MachineId}'", machineId);
            bool result = await _healthRepo.SaveHealthAsync(health, ct);
            if (result)
            {
                _cache.SetHealth(machineId, health);
            }
            return result;
        }

        /// <inheritdoc />
        public Task<bool> IsVersionCompatibleAsync(string machineId, string clientSemVer, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(clientSemVer))
            {
                _logger.LogWarning("Incompatible machine '{MachineId}' version: NULL or Empty", machineId);
                return Task.FromResult(false);
            }

            try
            {
                var version = new Version(clientSemVer.Split('-')[0]); // Handle pre-release tags like "1.0.0-beta"
                var minRequired = new Version("1.0.0");

                bool isCompatible = version >= minRequired;
                if (!isCompatible)
                {
                    _logger.LogWarning("Machine '{MachineId}' version '{SemVer}' is incompatible. Min required: '1.0.0'", machineId, clientSemVer);
                }

                return Task.FromResult(isCompatible);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse and check compatibility of version string '{SemVer}' for machine '{MachineId}'", clientSemVer, machineId);
                return Task.FromResult(false);
            }
        }
    }
}
