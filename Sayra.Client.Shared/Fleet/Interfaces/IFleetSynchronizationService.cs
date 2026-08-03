using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Shared.Models.Phase9.Domain;

namespace Sayra.Client.Shared.Fleet.Interfaces
{
    /// <summary>
    /// Contract for reconciling distributed workstation states, conflicts, and version drift checks.
    /// </summary>
    public interface IFleetSynchronizationService
    {
        /// <summary>
        /// Synchronizes and reconciles a workstation's core state using Last-Write-Wins conflict resolution.
        /// </summary>
        Task<bool> SynchronizeMachineStateAsync(MachineInfo localState, MachineInfo serverState, CancellationToken ct = default);

        /// <summary>
        /// Synchronizes state snapshots.
        /// </summary>
        Task<bool> SynchronizeSnapshotAsync(string machineId, MachineSnapshot snapshot, CancellationToken ct = default);

        /// <summary>
        /// Synchronizes inventory specs.
        /// </summary>
        Task<bool> SynchronizeInventoryAsync(string machineId, MachineInventory inventory, CancellationToken ct = default);

        /// <summary>
        /// Synchronizes current health scores.
        /// </summary>
        Task<bool> SynchronizeHealthAsync(string machineId, MachineHealth health, CancellationToken ct = default);

        /// <summary>
        /// Evaluates if a workstation's version matches organizational compatibility standards.
        /// </summary>
        Task<bool> IsVersionCompatibleAsync(string machineId, string clientSemVer, CancellationToken ct = default);
    }
}
