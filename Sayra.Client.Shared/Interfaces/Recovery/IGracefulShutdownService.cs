using System;
using System.Threading;
using System.Threading.Tasks;

namespace Sayra.Client.Shared.Interfaces.Recovery
{
    /// <summary>
    /// Contract for executing the safe, orderly, step-by-step graceful shutdown sequence of the client host.
    /// </summary>
    public interface IGracefulShutdownService
    {
        /// <summary>
        /// Commences the orderly multi-step graceful shutdown sequence within the specified timeout limit.
        /// </summary>
        /// <param name="timeout">The maximum duration allowed for the graceful shutdown sequence before forcing exit.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A Task representing the graceful shutdown process.</returns>
        Task InitiateShutdownAsync(TimeSpan timeout, CancellationToken cancellationToken = default);
    }
}
