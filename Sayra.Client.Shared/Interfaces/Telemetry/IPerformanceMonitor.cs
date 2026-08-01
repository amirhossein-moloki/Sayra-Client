using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Shared.Models.Telemetry;

namespace Sayra.Client.Shared.Interfaces.Telemetry
{
    /// <summary>
    /// Service responsible for monitoring system runtime performance.
    /// </summary>
    public interface IPerformanceMonitor
    {
        /// <summary>
        /// Asynchronously records a new performance snapshot containing platform latency metrics.
        /// </summary>
        /// <param name="snapshot">The performance snapshot details.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task RecordPerformanceSnapshotAsync(PerformanceSnapshot snapshot, CancellationToken cancellationToken = default);

        /// <summary>
        /// Asynchronously retrieves the latest performance snapshot.
        /// </summary>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The performance snapshot details.</returns>
        Task<PerformanceSnapshot> GetLatestPerformanceSnapshotAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Starts a thread-safe reusable performance measurement scope.
        /// </summary>
        /// <param name="operationName">The name of the operation being measured.</param>
        /// <returns>An active performance measurement scope.</returns>
        IPerformanceMeasurement StartMeasurement(string operationName);

        /// <summary>
        /// Records a completed performance measurement in the monitor's metrics registry.
        /// </summary>
        /// <param name="measurement">The completed measurement.</param>
        void RecordMeasurement(IPerformanceMeasurement measurement);
    }
}
