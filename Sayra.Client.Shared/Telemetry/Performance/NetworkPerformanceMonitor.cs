using System;
using System.Threading;
using Sayra.Client.Shared.Interfaces.Telemetry;

namespace Sayra.Client.Shared.Telemetry.Performance
{
    /// <summary>
    /// Performance monitoring wrapper for network operations.
    /// Observes TCP latency, download/upload throughput speed, and connection failures.
    /// </summary>
    public class NetworkPerformanceMonitor
    {
        private readonly IPerformanceMonitor _performanceMonitor;
        private long _connectionFailures;

        /// <summary>
        /// Gets the total count of observed network connection failures.
        /// </summary>
        public long ConnectionFailures => Interlocked.Read(ref _connectionFailures);

        /// <summary>
        /// Initializes a new instance of the <see cref="NetworkPerformanceMonitor"/> class.
        /// </summary>
        public NetworkPerformanceMonitor(IPerformanceMonitor performanceMonitor)
        {
            _performanceMonitor = performanceMonitor ?? throw new ArgumentNullException(nameof(performanceMonitor));
        }

        /// <summary>
        /// Starts a measurement for tracking TCP request or server connection latency.
        /// </summary>
        public IPerformanceMeasurement TrackTcpLatency()
        {
            return _performanceMonitor.StartMeasurement("Tcp.Latency");
        }

        /// <summary>
        /// Records the current download throughput speed.
        /// </summary>
        /// <param name="bytesPerSecond">The download speed in bytes per second.</param>
        public void RecordDownloadThroughput(double bytesPerSecond)
        {
            if (_performanceMonitor is PerformanceMonitor pm)
            {
                pm.RecordDownloadSpeed(bytesPerSecond);
            }
        }

        /// <summary>
        /// Records the current upload throughput speed.
        /// </summary>
        /// <param name="bytesPerSecond">The upload speed in bytes per second.</param>
        public void RecordUploadThroughput(double bytesPerSecond)
        {
            if (_performanceMonitor is PerformanceMonitor pm)
            {
                pm.RecordUploadSpeed(bytesPerSecond);
            }
        }

        /// <summary>
        /// Increments the observed network connection failures count.
        /// </summary>
        public void RecordConnectionFailure()
        {
            Interlocked.Increment(ref _connectionFailures);
        }
    }
}
