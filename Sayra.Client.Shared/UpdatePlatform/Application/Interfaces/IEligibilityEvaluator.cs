using System;
using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Shared.UpdatePlatform.Domain.Models;

namespace Sayra.Client.Shared.UpdatePlatform.Application.Interfaces
{
    /// <summary>
    /// Contract for evaluating a workstation's complete eligibility for a specific update.
    /// </summary>
    public interface IEligibilityEvaluator
    {
        /// <summary>
        /// Compiles and evaluates all rules (version limits, active sessions, windows, rings, etc.)
        /// to determine whether an update installation may proceed.
        /// </summary>
        /// <param name="manifest">The update manifest to check.</param>
        /// <param name="hasActiveSession">Whether there is currently an active user/game session on the PC.</param>
        /// <param name="hasPendingOperations">Whether there are other pending operations (such as diagnostic reboots or file sweeps).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A detailed EligibilityResult containing verdict and reasons.</returns>
        Task<EligibilityResult> EvaluateEligibilityAsync(
            UpdateManifest manifest,
            bool hasActiveSession,
            bool hasPendingOperations,
            CancellationToken cancellationToken);
    }
}
