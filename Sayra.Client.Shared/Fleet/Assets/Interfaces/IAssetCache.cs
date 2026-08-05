using System;
using Sayra.Client.Shared.Models.Phase9.Domain;

namespace Sayra.Client.Shared.Fleet.Assets.Interfaces
{
    /// <summary>
    /// Thread-safe in-memory cache for asset records with timed expiration.
    /// </summary>
    public interface IAssetCache
    {
        /// <summary>
        /// Caches an asset record with optional custom sliding/absolute expiration TTL.
        /// </summary>
        void Set(string assetId, AssetRecord asset, TimeSpan? expiration = null);

        /// <summary>
        /// Retrieves a cached asset record if it exists and has not expired.
        /// </summary>
        AssetRecord? Get(string assetId);

        /// <summary>
        /// Evicts a specific asset record from the cache.
        /// </summary>
        void Invalidate(string assetId);

        /// <summary>
        /// Completely clears all cached asset records.
        /// </summary>
        void Clear();
    }
}
