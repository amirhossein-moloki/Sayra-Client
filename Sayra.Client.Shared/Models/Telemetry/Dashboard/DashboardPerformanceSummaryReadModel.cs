using System;

namespace Sayra.Client.Shared.Models.Telemetry
{
    /// <summary>
    /// Represents an immutable consolidated report of workstation latencies, speeds, and CLR performance metrics.
    /// </summary>
    public record DashboardPerformanceSummaryReadModel
    {
        /// <summary>
        /// Gets the CPU usage percentage (0.0 to 100.0).
        /// </summary>
        public double CpuUsagePercent { get; init; }

        /// <summary>
        /// Gets the memory usage percentage (0.0 to 100.0).
        /// </summary>
        public double MemoryUsagePercent { get; init; }

        /// <summary>
        /// Gets the average response latency of database operations in milliseconds.
        /// </summary>
        public double DatabaseLatencyMs { get; init; }

        /// <summary>
        /// Gets the average response latency of local Named Pipe IPC communications in milliseconds.
        /// </summary>
        public double IpcLatencyMs { get; init; }

        /// <summary>
        /// Gets the average response latency of server TCP connection operations in milliseconds.
        /// </summary>
        public double TcpLatencyMs { get; init; }

        /// <summary>
        /// Gets the response latency of local storage disk I/O operations in milliseconds.
        /// </summary>
        public double DiskLatencyMs { get; init; }

        /// <summary>
        /// Gets the database/payload memory cache hit ratio (0.0 to 1.0).
        /// </summary>
        public double CacheHitRatio { get; init; }

        /// <summary>
        /// Gets the current download speed across update pipelines in bytes per second.
        /// </summary>
        public double DownloadSpeedBytesPerSec { get; init; }

        /// <summary>
        /// Gets the current upload speed across update pipelines in bytes per second.
        /// </summary>
        public double UploadSpeedBytesPerSec { get; init; }

        /// <summary>
        /// Gets the current length of the offline persistent transmission queue.
        /// </summary>
        public int QueueLength { get; init; }

        /// <summary>
        /// Gets the number of active thread pool threads.
        /// </summary>
        public int ThreadPoolThreads { get; init; }

        /// <summary>
        /// Gets the count of concurrent pending asynchronous operations.
        /// </summary>
        public int AsyncOperationsCount { get; init; }

        /// <summary>
        /// Gets the number of garbage collection cycles executed.
        /// </summary>
        public int GarbageCollectionCount { get; init; }

        /// <summary>
        /// Gets the exact timestamp when this read model was generated.
        /// </summary>
        public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    }
}
