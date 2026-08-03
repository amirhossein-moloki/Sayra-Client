using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Shared.Models.Phase9.Domain;

namespace Sayra.Client.Shared.Fleet.Interfaces
{
    /// <summary>
    /// Repository managing searching, assigning and deleting workstation metadata tags.
    /// </summary>
    public interface ITagRepository
    {
        /// <summary>
        /// Assigns a tag to a machine.
        /// </summary>
        Task<bool> AssignTagAsync(string machineId, FleetTag tag, CancellationToken ct = default);

        /// <summary>
        /// Removes a specific tag key from a machine.
        /// </summary>
        Task<bool> RemoveTagAsync(string machineId, string key, CancellationToken ct = default);

        /// <summary>
        /// Gets all tags applied to a specific machine.
        /// </summary>
        Task<IReadOnlyList<FleetTag>> GetTagsForMachineAsync(string machineId, CancellationToken ct = default);

        /// <summary>
        /// Retrieves all machine IDs having a specific tag key and value.
        /// </summary>
        Task<IReadOnlyList<string>> GetMachineIdsWithTagAsync(string key, string value, CancellationToken ct = default);

        /// <summary>
        /// Gets all active tags in the system.
        /// </summary>
        Task<IReadOnlyList<FleetTag>> GetAllTagsAsync(CancellationToken ct = default);
    }
}
