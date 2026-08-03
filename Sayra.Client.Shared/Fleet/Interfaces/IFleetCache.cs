using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Shared.Models.Phase9.Domain;

namespace Sayra.Client.Shared.Fleet.Interfaces
{
    /// <summary>
    /// Thread-safe enterprise cache for fast workstation lookups and memory metrics queries.
    /// </summary>
    public interface IFleetCache
    {
        /// <summary>
        /// Cache or update a workstation record.
        /// </summary>
        void SetMachine(MachineInfo machine);

        /// <summary>
        /// Retrieves a cached workstation.
        /// </summary>
        MachineInfo? GetMachine(string machineId);

        /// <summary>
        /// Retrieves all cached workstations.
        /// </summary>
        IReadOnlyList<MachineInfo> GetAllMachines();

        /// <summary>
        /// Removes a workstation from cache.
        /// </summary>
        void InvalidateMachine(string machineId);

        /// <summary>
        /// Cache or update a fleet group.
        /// </summary>
        void SetGroup(FleetGroup group);

        /// <summary>
        /// Retrieves a cached group.
        /// </summary>
        FleetGroup? GetGroup(string groupId);

        /// <summary>
        /// Retrieves all cached fleet groups.
        /// </summary>
        IReadOnlyList<FleetGroup> GetAllGroups();

        /// <summary>
        /// Removes a group from cache.
        /// </summary>
        void InvalidateGroup(string groupId);

        /// <summary>
        /// Cache or update a workstation's current state snapshot.
        /// </summary>
        void SetSnapshot(string machineId, MachineSnapshot snapshot);

        /// <summary>
        /// Retrieves a cached workstation snapshot.
        /// </summary>
        MachineSnapshot? GetSnapshot(string machineId);

        /// <summary>
        /// Removes a snapshot from cache.
        /// </summary>
        void InvalidateSnapshot(string machineId);

        /// <summary>
        /// Cache or update a workstation's current health scores.
        /// </summary>
        void SetHealth(string machineId, MachineHealth health);

        /// <summary>
        /// Retrieves cached health scores.
        /// </summary>
        MachineHealth? GetHealth(string machineId);

        /// <summary>
        /// Removes health scores from cache.
        /// </summary>
        void InvalidateHealth(string machineId);

        /// <summary>
        /// Cache or update a workstation's hardware and software asset inventory details.
        /// </summary>
        void SetInventory(string machineId, MachineInventory inventory);

        /// <summary>
        /// Retrieves cached inventory details.
        /// </summary>
        MachineInventory? GetInventory(string machineId);

        /// <summary>
        /// Removes inventory details from cache.
        /// </summary>
        void InvalidateInventory(string machineId);

        /// <summary>
        /// Warm up or refresh the cache completely from database persistence storage.
        /// </summary>
        Task RefreshAllAsync(CancellationToken ct = default);

        /// <summary>
        /// Clears all items in the cache.
        /// </summary>
        void Clear();
    }
}
