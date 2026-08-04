using System;
using Sayra.Client.Shared.Models.Phase9.Domain;

namespace Sayra.Client.Shared.Fleet.Assets.Interfaces
{
    /// <summary>
    /// Thread-safe in-memory cache for machine inventory specs with timed expiration.
    /// </summary>
    public interface IInventoryCache
    {
        /// <summary>
        /// Caches a machine inventory specification with optional custom expiration TTL.
        /// </summary>
        void Set(string machineId, MachineInventory inventory, TimeSpan? expiration = null);

        /// <summary>
        /// Retrieves cached machine inventory specs if they exist and have not expired.
        /// </summary>
        MachineInventory? Get(string machineId);

        /// <summary>
        /// Evicts specific machine inventory specs from the cache.
        /// </summary>
        void Invalidate(string machineId);

        /// <summary>
        /// Completely clears all cached machine inventory specs.
        /// </summary>
        void Clear();
    }
}
