using System;
using Sayra.Client.Shared.Models.Phase9.Domain;

namespace Sayra.Client.Shared.Fleet.Maintenance.Interfaces
{
    /// <summary>
    /// Thread-safe in-memory cache for maintenance schedules with timed expiration.
    /// </summary>
    public interface IMaintenanceCache
    {
        /// <summary>
        /// Caches a maintenance schedule with optional custom expiration TTL.
        /// </summary>
        void Set(string scheduleId, MaintenanceSchedule schedule, TimeSpan? expiration = null);

        /// <summary>
        /// Retrieves a cached maintenance schedule if it exists and has not expired.
        /// </summary>
        MaintenanceSchedule? Get(string scheduleId);

        /// <summary>
        /// Evicts a specific maintenance schedule from the cache.
        /// </summary>
        void Invalidate(string scheduleId);

        /// <summary>
        /// Completely clears all cached maintenance schedules.
        /// </summary>
        void Clear();
    }
}
