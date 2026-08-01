using System;
using System.Diagnostics;
using System.Threading;
using Sayra.Client.Shared.Interfaces.Telemetry;

namespace Sayra.Client.Shared.Telemetry.Performance
{
    /// <summary>
    /// Monitors .NET runtime performance metrics such as Garbage Collection collections,
    /// managed memory, ThreadPool workers, queue pressure, and active asynchronous operations.
    /// </summary>
    public class RuntimePerformanceMonitor
    {
        private readonly IPerformanceMonitor _performanceMonitor;

        /// <summary>
        /// Initializes a new instance of the <see cref="RuntimePerformanceMonitor"/> class.
        /// </summary>
        public RuntimePerformanceMonitor(IPerformanceMonitor performanceMonitor)
        {
            _performanceMonitor = performanceMonitor ?? throw new ArgumentNullException(nameof(performanceMonitor));
        }

        /// <summary>
        /// Gets the Gen 0 Garbage Collection count.
        /// </summary>
        public int Gen0Collections => GC.CollectionCount(0);

        /// <summary>
        /// Gets the Gen 1 Garbage Collection count.
        /// </summary>
        public int Gen1Collections => GC.CollectionCount(1);

        /// <summary>
        /// Gets the Gen 2 Garbage Collection count.
        /// </summary>
        public int Gen2Collections => GC.CollectionCount(2);

        /// <summary>
        /// Gets the total allocated managed memory in bytes.
        /// </summary>
        public long AllocatedMemoryBytes => GC.GetTotalMemory(false);

        /// <summary>
        /// Gets the number of available worker threads in the ThreadPool.
        /// </summary>
        public int AvailableWorkerThreads
        {
            get
            {
                ThreadPool.GetAvailableThreads(out int workerThreads, out _);
                return workerThreads;
            }
        }

        /// <summary>
        /// Gets the number of busy/active worker threads in the ThreadPool.
        /// </summary>
        public int BusyWorkerThreads
        {
            get
            {
                ThreadPool.GetMaxThreads(out int maxThreads, out _);
                ThreadPool.GetAvailableThreads(out int availThreads, out _);
                return Math.Max(0, maxThreads - availThreads);
            }
        }

        /// <summary>
        /// Gets the ThreadPool queue pressure represented by the number of pending work items.
        /// </summary>
        public long ThreadPoolQueuePressure
        {
            get
            {
                try
                {
                    return ThreadPool.PendingWorkItemCount;
                }
                catch
                {
                    return 0; // fallback if unsupported
                }
            }
        }

        /// <summary>
        /// Gets the count of concurrent active asynchronous operations being monitored.
        /// </summary>
        public int ActiveAsyncOperationsCount
        {
            get
            {
                if (_performanceMonitor is PerformanceMonitor pm)
                {
                    // Query directly from core PerformanceMonitor
                    var snapshotTask = pm.GetLatestPerformanceSnapshotAsync(CancellationToken.None);
                    if (snapshotTask.IsCompleted)
                    {
                        return snapshotTask.Result.AsyncOperationsCount;
                    }
                }
                return 0;
            }
        }

        /// <summary>
        /// Starts a performance measurement scope for tracking a generic asynchronous operation.
        /// </summary>
        public IPerformanceMeasurement TrackAsyncOperation(string operationName)
        {
            return _performanceMonitor.StartMeasurement($"Async.{operationName}");
        }
    }
}
