using System;
using System.Collections.Concurrent;
using Sayra.Client.Shared.Fleet.Assets.Interfaces;
using Sayra.Client.Shared.Models.Phase9.Domain;

namespace Sayra.Client.Shared.Fleet.Assets
{
    /// <summary>
    /// Implements <see cref="IInventoryCache"/> with concurrent thread safety and timed TTL expiration.
    /// </summary>
    public class InventoryCache : IInventoryCache
    {
        private class CacheItem
        {
            public MachineInventory Value { get; }
            public DateTime ExpirationTime { get; }

            public CacheItem(MachineInventory value, TimeSpan ttl)
            {
                Value = value;
                ExpirationTime = DateTime.UtcNow.Add(ttl);
            }

            public bool IsExpired => DateTime.UtcNow > ExpirationTime;
        }

        private readonly ConcurrentDictionary<string, CacheItem> _cache = new(StringComparer.OrdinalIgnoreCase);
        private readonly TimeSpan _defaultTtl = TimeSpan.FromMinutes(5);

        /// <inheritdoc />
        public void Set(string machineId, MachineInventory inventory, TimeSpan? expiration = null)
        {
            if (string.IsNullOrEmpty(machineId) || inventory == null) return;
            var ttl = expiration ?? _defaultTtl;
            _cache[machineId] = new CacheItem(inventory, ttl);
        }

        /// <inheritdoc />
        public MachineInventory? Get(string machineId)
        {
            if (string.IsNullOrEmpty(machineId)) return null;

            if (_cache.TryGetValue(machineId, out var item))
            {
                if (!item.IsExpired)
                {
                    return item.Value;
                }
                _cache.TryRemove(machineId, out _); // Lazy eviction on expired read
            }

            return null;
        }

        /// <inheritdoc />
        public void Invalidate(string machineId)
        {
            if (string.IsNullOrEmpty(machineId)) return;
            _cache.TryRemove(machineId, out _);
        }

        /// <inheritdoc />
        public void Clear()
        {
            _cache.Clear();
        }
    }
}
