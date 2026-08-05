using System;
using System.Collections.Concurrent;
using Sayra.Client.Shared.Fleet.Assets.Interfaces;
using Sayra.Client.Shared.Models.Phase9.Domain;

namespace Sayra.Client.Shared.Fleet.Assets
{
    /// <summary>
    /// Implements <see cref="IAssetCache"/> with concurrent thread safety and timed TTL expiration.
    /// </summary>
    public class AssetCache : IAssetCache
    {
        private class CacheItem
        {
            public AssetRecord Value { get; }
            public DateTime ExpirationTime { get; }

            public CacheItem(AssetRecord value, TimeSpan ttl)
            {
                Value = value;
                ExpirationTime = DateTime.UtcNow.Add(ttl);
            }

            public bool IsExpired => DateTime.UtcNow > ExpirationTime;
        }

        private readonly ConcurrentDictionary<string, CacheItem> _cache = new(StringComparer.OrdinalIgnoreCase);
        private readonly TimeSpan _defaultTtl = TimeSpan.FromMinutes(5);

        /// <inheritdoc />
        public void Set(string assetId, AssetRecord asset, TimeSpan? expiration = null)
        {
            if (string.IsNullOrEmpty(assetId) || asset == null) return;
            var ttl = expiration ?? _defaultTtl;
            _cache[assetId] = new CacheItem(asset, ttl);
        }

        /// <inheritdoc />
        public AssetRecord? Get(string assetId)
        {
            if (string.IsNullOrEmpty(assetId)) return null;

            if (_cache.TryGetValue(assetId, out var item))
            {
                if (!item.IsExpired)
                {
                    return item.Value;
                }
                _cache.TryRemove(assetId, out _); // Lazy eviction on expired read
            }

            return null;
        }

        /// <inheritdoc />
        public void Invalidate(string assetId)
        {
            if (string.IsNullOrEmpty(assetId)) return;
            _cache.TryRemove(assetId, out _);
        }

        /// <inheritdoc />
        public void Clear()
        {
            _cache.Clear();
        }
    }
}
