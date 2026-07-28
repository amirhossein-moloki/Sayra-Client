using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Shared.UpdatePlatform.Domain.Models;

namespace Sayra.Client.Shared.UpdatePlatform.Application.Interfaces
{
    /// <summary>
    /// Contract representing the client-side gateway for administrative telemetry and diagnostic integration.
    /// </summary>
    public interface IAdminIntegrationClient
    {
        /// <summary>
        /// Sends a telemetry event to the administration backend.
        /// </summary>
        /// <param name="telemetryEvent">The telemetry event details.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>True if successfully transmitted; false otherwise.</returns>
        Task<bool> ReportTelemetryEventAsync(UpdateTelemetryEvent telemetryEvent, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sends a health status metric update to the administrative backend.
        /// </summary>
        /// <param name="healthMetric">The health metric details.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>True if successfully transmitted; false otherwise.</returns>
        Task<bool> ReportHealthMetricAsync(HealthMetric healthMetric, CancellationToken cancellationToken = default);
    }
}
