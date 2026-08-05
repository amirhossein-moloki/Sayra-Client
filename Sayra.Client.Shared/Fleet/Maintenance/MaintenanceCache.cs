using System;
using System.Collections.Concurrent;
using Sayra.Client.Shared.Fleet.Maintenance.Interfaces;
using Sayra.Client.Shared.Models.Phase9.Domain;

namespace Sayra.Client.Shared.Fleet.Maintenance
{
    /// <summary>
    /// Implements <see cref="IMaintenanceCache"/> with concurrent thread safety and timed TTL expiration.
    /// </summary>
    public class MaintenanceCache : IMaintenanceCache
    {
        private class CacheItem
        {
            public MaintenanceSchedule Value { get; }
            public DateTime ExpirationTime { get; }

            public CacheItem(MaintenanceSchedule value, TimeSpan ttl)
            {
                Value = value;
                ExpirationTime = DateTime.UtcNow.Add(ttl);
            }

            public bool IsExpired => DateTime.UtcNow > ExpirationTime;
        }

        private readonly ConcurrentDictionary<string, CacheItem> _cache = new(StringComparer.OrdinalIgnoreCase);
        private readonly TimeSpan _defaultTtl = TimeSpan.FromMinutes(5);

        /// <inheritdoc />
        public void Set(string scheduleId, MaintenanceSchedule schedule, TimeSpan? expiration = null)
        {
            if (string.IsNullOrEmpty(scheduleId) || schedule == null) return;
            var ttl = expiration ?? _defaultTtl;
            _cache[scheduleId] = new CacheItem(schedule, ttl);
        }

        /// <inheritdoc />
        public MaintenanceSchedule? Get(string scheduleId)
        {
            if (string.IsNullOrEmpty(scheduleId)) return null;

            if (_cache.TryGetValue(scheduleId, out var item))
            {
                if (!item.IsExpired)
                {
                    return item.Value;
                }
                _cache.TryRemove(scheduleId, out _); // Lazy eviction on expired read
            }

            return null;
        }

        /// <inheritdoc />
        public void Invalidate(string scheduleId)
        {
            if (string.IsNullOrEmpty(scheduleId)) return;
            _cache.TryRemove(scheduleId, out _);
        }

        /// <inheritdoc />
        public void Clear()
        {
            _cache.Clear();
        }
    }
}
