using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Shared.Models.Phase9.Domain;

namespace Sayra.Client.Shared.Fleet.Interfaces
{
    /// <summary>
    /// Repository managing workstation groups and static/dynamic memberships.
    /// </summary>
    public interface IGroupRepository
    {
        /// <summary>
        /// Saves or updates a fleet group.
        /// </summary>
        Task<bool> SaveGroupAsync(FleetGroup group, CancellationToken ct = default);

        /// <summary>
        /// Deletes an existing fleet group.
        /// </summary>
        Task<bool> DeleteGroupAsync(string groupId, CancellationToken ct = default);

        /// <summary>
        /// Retrieves a group by identifier.
        /// </summary>
        Task<FleetGroup?> GetGroupAsync(string groupId, CancellationToken ct = default);

        /// <summary>
        /// Gets all registered fleet groups.
        /// </summary>
        Task<IReadOnlyList<FleetGroup>> GetAllGroupsAsync(CancellationToken ct = default);

        /// <summary>
        /// Adds a workstation to a group (Static membership).
        /// </summary>
        Task<bool> AssignMachineAsync(string machineId, string groupId, CancellationToken ct = default);

        /// <summary>
        /// Removes a workstation from a group.
        /// </summary>
        Task<bool> RemoveMachineAsync(string machineId, string groupId, CancellationToken ct = default);

        /// <summary>
        /// Retrieves all machine IDs associated with a specific group.
        /// </summary>
        Task<IReadOnlyList<string>> GetMachineIdsInGroupAsync(string groupId, CancellationToken ct = default);

        /// <summary>
        /// Retrieves all group IDs associated with a specific workstation.
        /// </summary>
        Task<IReadOnlyList<string>> GetGroupIdsForMachineAsync(string machineId, CancellationToken ct = default);

        /// <summary>
        /// Atomically synchronizes membership for a specific group.
        /// </summary>
        Task<bool> SyncGroupMembershipsAsync(string groupId, IEnumerable<string> machineIds, CancellationToken ct = default);
    }
}
