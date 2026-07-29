using System.Threading;
using System.Threading.Tasks;

namespace Sayra.Client.Shared.Interfaces.Recovery
{
    /// <summary>
    /// Contract for generating and persisting structured diagnostics, stability, and failure reports.
    /// </summary>
    public interface IRecoveryDiagnosticsEngine
    {
        /// <summary>
        /// Generates and persists startup, health, recovery, and failure summary reports asynchronously.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A Task representing the batch generation process.</returns>
        Task GenerateAndPersistAllReportsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Generates a report focused on workstation startup checks and operating environment.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A Task returning the generated startup report as a string.</returns>
        Task<string> GenerateStartupReportAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Generates a comprehensive periodic health status summary across all registered subsystems.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A Task returning the generated health report as a string.</returns>
        Task<string> GenerateHealthSummaryReportAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Generates a report compiling all self-healing events, backoff states, and recovery attempts.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A Task returning the generated recovery report as a string.</returns>
        Task<string> GenerateRecoveryReportAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Generates a specialized report focusing on currently active failure states or recent exceptions.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A Task returning the generated failure report as a string.</returns>
        Task<string> GenerateFailureReportAsync(CancellationToken cancellationToken = default);
    }
}
