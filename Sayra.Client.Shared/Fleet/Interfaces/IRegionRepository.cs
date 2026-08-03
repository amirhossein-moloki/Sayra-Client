using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Shared.Models.Phase9.Domain;

namespace Sayra.Client.Shared.Fleet.Interfaces
{
    /// <summary>
    /// Repository managing geographic regions and centers.
    /// </summary>
    public interface IRegionRepository
    {
        /// <summary>
        /// Saves or updates a region.
        /// </summary>
        Task<bool> SaveAsync(FleetRegion region, string? parentRegionId, CancellationToken ct = default);

        /// <summary>
        /// Deletes a region.
        /// </summary>
        Task<bool> DeleteAsync(string regionId, CancellationToken ct = default);

        /// <summary>
        /// Retrieves a specific region.
        /// </summary>
        Task<FleetRegion?> GetAsync(string regionId, CancellationToken ct = default);

        /// <summary>
        /// Retrieves the parent region ID for a given region.
        /// </summary>
        Task<string?> GetParentRegionIdAsync(string regionId, CancellationToken ct = default);

        /// <summary>
        /// Gets all registered regions.
        /// </summary>
        Task<IReadOnlyList<FleetRegion>> GetAllAsync(CancellationToken ct = default);
    }
}
