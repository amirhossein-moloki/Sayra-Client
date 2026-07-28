using System;
using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Shared.UpdatePlatform.Domain.Models;

namespace Sayra.Client.Shared.UpdatePlatform.Application.Interfaces
{
    /// <summary>
    /// Contract responsible for tracking update component states, disk capacity thresholds, and system health status.
    /// </summary>
    public interface IHealthMonitor
    {
        /// <summary>
        /// Performs full validation on storage, database, engine states, and returns the aggregated health metric.
        /// </summary>
        Task<HealthMetric> EvaluateHealthAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets a value indicating whether all core components are functional.
        /// </summary>
        Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Queries the version code of the last successfully applied update package.
        /// </summary>
        Task<string> GetLastSuccessfulUpdateVersionAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Queries the UTC timestamp of the last successfully applied update.
        /// </summary>
        Task<DateTime?> GetLastSuccessfulUpdateUtcAsync(CancellationToken cancellationToken = default);
    }
}
