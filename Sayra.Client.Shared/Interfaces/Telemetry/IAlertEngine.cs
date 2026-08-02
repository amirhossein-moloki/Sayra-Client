using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Shared.Models.Telemetry;

namespace Sayra.Client.Shared.Interfaces.Telemetry
{
    /// <summary>
    /// Service responsible for evaluating threshold rules, deduplicating alerts, and raising system notifications.
    /// </summary>
    public interface IAlertEngine
    {
        /// <summary>
        /// Asynchronously processes a single alert record, applying rule evaluations and routing workflows.
        /// </summary>
        /// <param name="alert">The alert record to process.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task ProcessAlertAsync(AlertRecord alert, CancellationToken cancellationToken = default);

        /// <summary>
        /// Asynchronously retrieves all currently active alerts that are not yet resolved.
        /// </summary>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>A collection of active alert records.</returns>
        Task<IReadOnlyCollection<AlertRecord>> GetActiveAlertsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Asynchronously acknowledges an active alert.
        /// </summary>
        /// <param name="alertId">The unique ID of the alert.</param>
        /// <param name="operatorId">The ID of the operator acknowledging the alert.</param>
        /// <param name="comment">The optional comment left by the operator.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task AcknowledgeAlertAsync(string alertId, string operatorId, string? comment = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Asynchronously resolves an active alert.
        /// </summary>
        /// <param name="alertId">The unique ID of the alert.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task ResolveAlertAsync(string alertId, CancellationToken cancellationToken = default);
    }
}
