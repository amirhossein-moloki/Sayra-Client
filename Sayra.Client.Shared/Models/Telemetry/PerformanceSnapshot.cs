using System;

namespace Sayra.Client.Shared.Models.Telemetry
{
    /// <summary>
    /// Represents a detailed snapshot of system performance and resource latency metrics.
    /// </summary>
    public record PerformanceSnapshot
    {
        /// <summary>
        /// Gets the snapshot creation timestamp.
        /// </summary>
        public DateTime Timestamp { get; init; } = DateTime.UtcNow;

        /// <summary>
        /// Gets the overall application startup duration.
        /// </summary>
        public TimeSpan StartupTime { get; init; } = TimeSpan.Zero;

        /// <summary>
        /// Gets the duration of the last authentication request.
        /// </summary>
        public TimeSpan AuthenticationTime { get; init; } = TimeSpan.Zero;

        /// <summary>
        /// Gets the average response latency of database operations.
        /// </summary>
        public TimeSpan DatabaseLatency { get; init; } = TimeSpan.Zero;

        /// <summary>
        /// Gets the average response latency of local Named Pipe IPC communications.
        /// </summary>
        public TimeSpan IpcLatency { get; init; } = TimeSpan.Zero;

        /// <summary>
        /// Gets the average response latency of server TCP connection operations.
        /// </summary>
        public TimeSpan TcpLatency { get; init; } = TimeSpan.Zero;

        /// <summary>
        /// Gets the download speed in bytes per second.
        /// </summary>
        public double DownloadSpeed { get; init; }

        /// <summary>
        /// Gets the upload speed in bytes per second.
        /// </summary>
        public double UploadSpeed { get; init; }

        /// <summary>
        /// Gets the response latency of local storage disk I/O operations.
        /// </summary>
        public TimeSpan DiskLatency { get; init; } = TimeSpan.Zero;

        /// <summary>
        /// Gets the database/payload memory cache hit ratio (0.0 to 1.0).
        /// </summary>
        public double CacheHitRatio { get; init; }

        /// <summary>
        /// Gets the current length of the offline persistent transmission queue.
        /// </summary>
        public int QueueLength { get; init; }

        /// <summary>
        /// Gets the execution time of the background workers.
        /// </summary>
        public TimeSpan WorkerExecutionTime { get; init; } = TimeSpan.Zero;

        /// <summary>
        /// Gets the number of garbage collection cycles executed.
        /// </summary>
        public int GarbageCollectionCount { get; init; }

        /// <summary>
        /// Gets the number of active thread pool threads.
        /// </summary>
        public int ThreadPoolThreads { get; init; }

        /// <summary>
        /// Gets the count of concurrent pending asynchronous operations.
        /// </summary>
        public int AsyncOperationsCount { get; init; }

        // --- Step 9 Added Properties for Tracing and Single-Operation Snapshots ---

        /// <summary>
        /// Gets the target machine ID where the snapshot or operation took place.
        /// </summary>
        public string MachineId { get; init; } = Environment.MachineName;

        /// <summary>
        /// Gets the subsystem being measured in this snapshot, if applicable.
        /// </summary>
        public string? Subsystem { get; init; }

        /// <summary>
        /// Gets the name of the operation measured in this snapshot, if applicable.
        /// </summary>
        public string? Operation { get; init; }

        /// <summary>
        /// Gets the status or outcome of the measured operation (e.g. "Success", "Failed").
        /// </summary>
        public string? Status { get; init; }

        /// <summary>
        /// Gets the associated Trace ID, if applicable.
        /// </summary>
        public string? TraceId { get; init; }

        /// <summary>
        /// Gets the associated Correlation ID, if applicable.
        /// </summary>
        public string? CorrelationId { get; init; }

        /// <summary>
        /// Gets the duration of this specific snapshot/operation, if applicable.
        /// </summary>
        public TimeSpan Duration { get; init; } = TimeSpan.Zero;
    }
}
