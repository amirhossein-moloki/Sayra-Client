using System;
using System.Threading;
using Sayra.Client.Shared.Interfaces.Telemetry;

namespace Sayra.Client.Shared.Telemetry.Performance
{
    /// <summary>
    /// Performance monitoring wrapper for Named Pipe IPC communication.
    /// Observes Named Pipe latency, request/response durations, and timeout counts.
    /// </summary>
    public class IpcPerformanceMonitor
    {
        private readonly IPerformanceMonitor _performanceMonitor;
        private long _timeoutCount;

        /// <summary>
        /// Gets the total count of observed IPC request timeouts.
        /// </summary>
        public long TimeoutCount => Interlocked.Read(ref _timeoutCount);

        /// <summary>
        /// Initializes a new instance of the <see cref="IpcPerformanceMonitor"/> class.
        /// </summary>
        public IpcPerformanceMonitor(IPerformanceMonitor performanceMonitor)
        {
            _performanceMonitor = performanceMonitor ?? throw new ArgumentNullException(nameof(performanceMonitor));
        }

        /// <summary>
        /// Starts a measurement for tracking IPC request and response duration.
        /// </summary>
        public IPerformanceMeasurement TrackRequest(string operation)
        {
            return _performanceMonitor.StartMeasurement($"Ipc.Request:{operation}");
        }

        /// <summary>
        /// Starts a measurement for tracking low-level Named Pipe connection or write latency.
        /// </summary>
        public IPerformanceMeasurement TrackPipeLatency()
        {
            return _performanceMonitor.StartMeasurement("Ipc.PipeLatency");
        }

        /// <summary>
        /// Increments the observed IPC timeout count.
        /// </summary>
        public void RecordTimeout()
        {
            Interlocked.Increment(ref _timeoutCount);
        }
    }
}
