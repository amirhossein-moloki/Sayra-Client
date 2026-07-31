using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Shared.Models.Telemetry;

namespace Sayra.Client.Shared.Interfaces.Telemetry
{
    /// <summary>
    /// Service responsible for collecting, buffering, and dispatching all workstation telemetry records.
    /// </summary>
    public interface ITelemetryService
    {
        /// <summary>
        /// Asynchronously tracks a single telemetry record.
        /// </summary>
        /// <param name="record">The telemetry record to track.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task TrackMetricAsync(TelemetryRecord record, CancellationToken cancellationToken = default);

        /// <summary>
        /// Asynchronously starts the automatic background telemetry collection loops.
        /// </summary>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task StartCollectionAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Asynchronously stops the automatic background telemetry collection loops.
        /// </summary>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task StopCollectionAsync(CancellationToken cancellationToken = default);
    }
}
