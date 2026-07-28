using System;
using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Shared.UpdatePlatform.Application.Interfaces;
using Sayra.Client.Shared.UpdatePlatform.Domain.Models;

namespace Sayra.Client.Shared.UpdatePlatform.Application.Services
{
    /// <summary>
    /// Gateway client responsible for communicating with the server's administrative telemetry endpoints.
    /// Supports simulated network/API failures for testing and pipeline resilience verification.
    /// </summary>
    public class AdminIntegrationClient : IAdminIntegrationClient
    {
        /// <summary>
        /// Gets or sets a value indicating whether network/API requests should fail.
        /// Useful for testing local offline buffering and retry loops.
        /// </summary>
        public bool SimulateNetworkFailure { get; set; }

        public Task<bool> ReportTelemetryEventAsync(UpdateTelemetryEvent telemetryEvent, CancellationToken cancellationToken = default)
        {
            if (telemetryEvent == null) throw new ArgumentNullException(nameof(telemetryEvent));

            if (SimulateNetworkFailure)
            {
                return Task.FromResult(false);
            }

            // In production, this would serialise and HTTP POST to "POST https://update.sayra.io/api/v1/telemetry"
            return Task.FromResult(true);
        }

        public Task<bool> ReportHealthMetricAsync(HealthMetric healthMetric, CancellationToken cancellationToken = default)
        {
            if (healthMetric == null) throw new ArgumentNullException(nameof(healthMetric));

            if (SimulateNetworkFailure)
            {
                return Task.FromResult(false);
            }

            // In production, this would serialise and HTTP POST to "POST https://update.sayra.io/api/v1/health"
            return Task.FromResult(true);
        }
    }
}
