using System.Threading;
using System.Threading.Tasks;

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
    }
}
