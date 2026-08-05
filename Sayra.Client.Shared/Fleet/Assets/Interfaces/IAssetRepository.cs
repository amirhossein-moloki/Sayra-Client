using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Shared.Models.Phase9.Domain;

namespace Sayra.Client.Shared.Fleet.Assets.Interfaces
{
    /// <summary>
    /// Thread-safe enterprise SQLCipher storage repository for asset and inventory items.
    /// </summary>
    public interface IAssetRepository
    {
        /// <summary>
        /// Saves or updates an asset record.
        /// </summary>
        Task<bool> SaveAssetAsync(AssetRecord asset, CancellationToken ct = default);

        /// <summary>
        /// Retrieves a specific asset record by ID.
        /// </summary>
        Task<AssetRecord?> GetAssetAsync(string assetId, CancellationToken ct = default);

        /// <summary>
        /// Deletes a specific asset record by ID.
        /// </summary>
        Task<bool> DeleteAssetAsync(string assetId, CancellationToken ct = default);

        /// <summary>
        /// Retrieves all assets associated with a specific machine ID.
        /// </summary>
        Task<IReadOnlyList<AssetRecord>> GetAssetsByMachineAsync(string machineId, CancellationToken ct = default);

        /// <summary>
        /// Saves a point-in-time inventory snapshot of assets for a machine.
        /// </summary>
        Task<bool> SaveInventorySnapshotAsync(string machineId, IEnumerable<AssetRecord> assets, CancellationToken ct = default);

        /// <summary>
        /// Records an item of asset history.
        /// </summary>
        Task<bool> RecordHistoryAsync(AssetHistory history, CancellationToken ct = default);

        /// <summary>
        /// Retrieves asset history logs for a specific asset or machine.
        /// </summary>
        Task<IReadOnlyList<AssetHistory>> GetHistoryAsync(string? assetId = null, string? machineId = null, CancellationToken ct = default);

        /// <summary>
        /// Records an asset property change record.
        /// </summary>
        Task<bool> RecordChangeAsync(AssetChangeRecord change, CancellationToken ct = default);

        /// <summary>
        /// Retrieves asset change records for a specific asset or machine.
        /// </summary>
        Task<IReadOnlyList<AssetChangeRecord>> GetChangesAsync(string? assetId = null, string? machineId = null, CancellationToken ct = default);

        /// <summary>
        /// Searches across the asset catalog with advanced filtering, sorting, and pagination.
        /// </summary>
        Task<(IReadOnlyList<AssetRecord> Items, int TotalCount)> SearchAssetsAsync(
            string? machineId = null,
            string? assetType = null,
            string? serialNumber = null,
            string? version = null,
            string? manufacturer = null,
            string? driverVersion = null,
            string? softwareName = null,
            string? searchTerm = null,
            string? sortBy = null,
            bool ascending = true,
            int pageIndex = 0,
            int pageSize = 20,
            CancellationToken ct = default);
    }
}
