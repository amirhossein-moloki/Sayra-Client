using System;
using Sayra.Client.Shared.Interfaces.Telemetry;

namespace Sayra.Client.Shared.Telemetry.Performance
{
    /// <summary>
    /// Performance monitoring wrapper for cache systems.
    /// Observes cache hits and misses, and computes the hit ratio.
    /// </summary>
    public class CachePerformanceMonitor
    {
        private readonly IPerformanceMonitor _performanceMonitor;

        /// <summary>
        /// Initializes a new instance of the <see cref="CachePerformanceMonitor"/> class.
        /// </summary>
        public CachePerformanceMonitor(IPerformanceMonitor performanceMonitor)
        {
            _performanceMonitor = performanceMonitor ?? throw new ArgumentNullException(nameof(performanceMonitor));
        }

        /// <summary>
        /// Records a successful cache hit.
        /// </summary>
        public void RecordHit()
        {
            if (_performanceMonitor is PerformanceMonitor pm)
            {
                pm.RecordCacheHit();
            }
        }

        /// <summary>
        /// Records a cache miss.
        /// </summary>
        public void RecordMiss()
        {
            if (_performanceMonitor is PerformanceMonitor pm)
            {
                pm.RecordCacheMiss();
            }
        }
    }
}
