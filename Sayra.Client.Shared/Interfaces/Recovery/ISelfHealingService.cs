using System.Threading;
using System.Threading.Tasks;

namespace Sayra.Client.Shared.Interfaces.Recovery
{
    /// <summary>
    /// Contract for automatically executing self-healing, backoffs, and recovery procedures on failing subsystems.
    /// </summary>
    public interface ISelfHealingService
    {
        /// <summary>
        /// Scans all registered subsystems and initiates parallel self-healing recovery actions on any unhealthy/critical subsystems.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A Task representing the monitor and heal operation.</returns>
        Task MonitorAndHealAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Explicitly triggers a self-healing recovery operation on a specific subsystem.
        /// </summary>
        /// <param name="subsystemName">The name of the target subsystem to recover.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A Task representing the recovery operation.</returns>
        Task RecoverSubsystemAsync(string subsystemName, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the current number of recovery attempts executed for the specified subsystem.
        /// </summary>
        /// <param name="subsystemName">The name of the subsystem.</param>
        /// <returns>The number of attempts.</returns>
        int GetRecoveryAttemptsCount(string subsystemName);

        /// <summary>
        /// Gets the current number of recovery attempts executed for the specified subsystem asynchronously.
        /// </summary>
        /// <param name="subsystemName">The name of the subsystem.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A Task returning the number of attempts.</returns>
        Task<int> GetRecoveryAttemptsCountAsync(string subsystemName, CancellationToken cancellationToken = default);
    }
}
