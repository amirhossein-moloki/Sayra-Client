using System;
using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Shared.Models.Telemetry;

namespace Sayra.Client.Shared.Interfaces.Telemetry
{
    /// <summary>
    /// Service responsible for constructing and serving structured snapshot data to local/remote admin dashboards.
    /// </summary>
    public interface IDashboardProvider
    {
        /// <summary>
        /// Asynchronously retrieves the latest aggregated workstation and subsystem status snapshot.
        /// </summary>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>A comprehensive DashboardSnapshot model.</returns>
        Task<DashboardSnapshot> GetDashboardSnapshotAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Asynchronously sets up a subscription callback to receive streamed dashboard update notifications.
        /// </summary>
        /// <param name="onUpdate">The callback action executed when updates occur.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>A task representing the subscription lifetime.</returns>
        Task StreamDashboardUpdatesAsync(Action<DashboardSnapshot> onUpdate, CancellationToken cancellationToken = default);
    }
}
