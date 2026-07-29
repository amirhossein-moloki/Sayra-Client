using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Shared.Models.Recovery;

namespace Sayra.Client.Shared.Interfaces.Recovery
{
    /// <summary>
    /// Contract for executing automatic crash recovery, index repairs, and unfinished task restoration during workstation startup.
    /// </summary>
    public interface ICrashRecoveryManager
    {
        /// <summary>
        /// Orchestrates the full systematic startup recovery pipeline (e.g., db repairs, resume downloads, policies sync).
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A Task representing the asynchronous execution.</returns>
        Task ExecuteStartupRecoveryAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Verifies database structural consistency and performs index repairs or reindexing if necessary.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A Task representing the database check and repair.</returns>
        Task VerifyAndRepairDatabaseAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Validates the previous shutdown state to determine if the application was terminated unexpectedly.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The detected previous shutdown state information.</returns>
        Task<PreviousShutdownState> ValidatePreviousShutdownAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Detects and recovers interrupted operations such as downloads, updates, offline queue items, etc.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A list of recovery results for the interrupted operations.</returns>
        Task<List<RecoveryResult>> RecoverInterruptedOperationsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Restores state consistency for a specific subsystem.
        /// </summary>
        /// <param name="subsystemName">The name of the subsystem to recover.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The recovery outcome.</returns>
        Task<RecoveryResult> RecoverSubsystemStateAsync(string subsystemName, CancellationToken cancellationToken = default);

        /// <summary>
        /// Safely cleans up temporary and incomplete state files from the workstation storage.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A Task representing the cleanup operation.</returns>
        Task CleanupTemporaryStateAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Generates a comprehensive report detailing the crash recovery attempt and its outcomes.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A recovery report summarizing the operations.</returns>
        Task<RecoveryReport> GenerateRecoverySummaryAsync(CancellationToken cancellationToken = default);
    }
}
