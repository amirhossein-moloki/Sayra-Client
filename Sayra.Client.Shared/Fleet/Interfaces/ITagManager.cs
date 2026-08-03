using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Shared.Models.Phase9.Domain;

namespace Sayra.Client.Shared.Fleet.Interfaces
{
    /// <summary>
    /// Contract for workstation tag management and inheritance checks.
    /// </summary>
    public interface ITagManager
    {
        /// <summary>
        /// Assigns a tag to a machine.
        /// </summary>
        Task<bool> AssignTagAsync(string machineId, FleetTag tag, CancellationToken ct = default);

        /// <summary>
        /// Removes a tag from a machine.
        /// </summary>
        Task<bool> RemoveTagAsync(string machineId, string key, CancellationToken ct = default);

        /// <summary>
        /// Retrieves all tags assigned directly to a machine.
        /// </summary>
        Task<IReadOnlyList<FleetTag>> GetTagsForMachineAsync(string machineId, CancellationToken ct = default);

        /// <summary>
        /// Searches for machines matching a specific tag key and value.
        /// </summary>
        Task<IReadOnlyList<string>> SearchMachinesByTagAsync(string key, string value, CancellationToken ct = default);

        /// <summary>
        /// Retrieves all active tags globally registered.
        /// </summary>
        Task<IReadOnlyList<FleetTag>> GetAllTagsAsync(CancellationToken ct = default);

        /// <summary>
        /// Automatically evaluates and applies tags based on workstation state/metadata.
        /// </summary>
        Task EvaluateAutomaticTagsAsync(string machineId, CancellationToken ct = default);

        /// <summary>
        /// Computes inherited tags for a machine from its parent groups, regions, or departments.
        /// </summary>
        Task<IReadOnlyList<FleetTag>> GetInheritedTagsAsync(string machineId, CancellationToken ct = default);
    }
}
