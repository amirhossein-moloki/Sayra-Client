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

        /// <summary>
        /// Asynchronously retrieves the optimized high-level overview read model.
        /// </summary>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>A DashboardOverviewReadModel instance.</returns>
        Task<DashboardOverviewReadModel> GetOverviewAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Asynchronously retrieves the optimized subsystem status read model for all 15 systems.
        /// </summary>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>A DashboardSubsystemStatusReadModel instance.</returns>
        Task<DashboardSubsystemStatusReadModel> GetSubsystemStatusAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Asynchronously retrieves the optimized system and database performance summary read model.
        /// </summary>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>A DashboardPerformanceSummaryReadModel instance.</returns>
        Task<DashboardPerformanceSummaryReadModel> GetPerformanceSummaryAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Asynchronously retrieves the optimized alert summary read model representing current active alerts.
        /// </summary>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>A DashboardAlertSummaryReadModel instance.</returns>
        Task<DashboardAlertSummaryReadModel> GetAlertSummaryAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Asynchronously retrieves the optimized security validation and audit posture summary read model.
        /// </summary>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>A DashboardSecuritySummaryReadModel instance.</returns>
        Task<DashboardSecuritySummaryReadModel> GetSecuritySummaryAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Asynchronously retrieves the optimized recovery summary read model covering failures and self-healing.
        /// </summary>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>A DashboardRecoverySummaryReadModel instance.</returns>
        Task<DashboardRecoverySummaryReadModel> GetRecoverySummaryAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Asynchronously retrieves the optimized policy enforcement and update compliance summary read model.
        /// </summary>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>A DashboardComplianceSummaryReadModel instance.</returns>
        Task<DashboardComplianceSummaryReadModel> GetComplianceSummaryAsync(CancellationToken cancellationToken = default);
    }
}
