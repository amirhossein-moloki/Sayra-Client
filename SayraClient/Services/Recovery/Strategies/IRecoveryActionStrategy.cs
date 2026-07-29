using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Shared.Models.Recovery.Policies;

namespace SayraClient.Services.Recovery.Strategies
{
    /// <summary>
    /// Represents a pluggable recovery action strategy for a specific RecoveryActionType.
    /// </summary>
    public interface IRecoveryActionStrategy
    {
        /// <summary>
        /// Gets the recovery action type that this strategy handles.
        /// </summary>
        RecoveryActionType ActionType { get; }

        /// <summary>
        /// Executes the recovery action asynchronously.
        /// </summary>
        /// <param name="subsystemName">The name of the subsystem being recovered.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>True if the recovery was successful; otherwise, false.</returns>
        Task<bool> ExecuteAsync(string subsystemName, CancellationToken cancellationToken);
    }
}
