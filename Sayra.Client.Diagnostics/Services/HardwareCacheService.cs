using System;
using Microsoft.Extensions.Options;
using Sayra.Client.Diagnostics.Configuration;
using Sayra.Client.Diagnostics.Interfaces;
using Sayra.Client.Diagnostics.Models;

namespace Sayra.Client.Diagnostics.Services
{
    public class HardwareCacheService : IHardwareCacheService
    {
        private readonly IOptions<DiagnosticsOptions> _options;
        private readonly object _lock = new();
        private HardwareSpecification? _cachedSpec;
        private DateTime _cacheTime = DateTime.MinValue;

        public HardwareCacheService(IOptions<DiagnosticsOptions> options)
        {
            _options = options;
        }

        public HardwareSpecification? Get()
        {
            lock (_lock)
            {
                if (_cachedSpec == null || IsExpired())
                {
                    return null;
                }
                return _cachedSpec;
            }
        }

        public void Set(HardwareSpecification spec)
        {
            lock (_lock)
            {
                _cachedSpec = spec;
                _cacheTime = DateTime.UtcNow;
            }
        }

        public bool IsExpired()
        {
            lock (_lock)
            {
                if (_cachedSpec == null) return true;
                var duration = TimeSpan.FromMinutes(_options.Value.CacheDurationMinutes);
                return DateTime.UtcNow - _cacheTime > duration;
            }
        }

        public void Clear()
        {
            lock (_lock)
            {
                _cachedSpec = null;
                _cacheTime = DateTime.MinValue;
            }
        }
    }
}
