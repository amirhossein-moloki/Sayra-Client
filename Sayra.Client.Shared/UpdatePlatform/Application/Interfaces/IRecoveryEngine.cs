using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Shared.UpdatePlatform.Domain.Models;

namespace Sayra.Client.Shared.UpdatePlatform.Application.Interfaces
{
    /// <summary>
    /// Represents the automated recovery orchestrator for detecting failures and executing rollbacks.
    /// </summary>
    public interface IRecoveryEngine
    {
        /// <summary>
        /// Orchestrates the recovery pipeline on the given context.
        /// </summary>
        Task<RecoveryReport> RecoverAsync(RecoveryContext context, CancellationToken cancellationToken = default);

        /// <summary>
        /// Checks the health context, and triggers a full automatic rollback recovery if corruption or failure is detected.
        /// </summary>
        Task<bool> DetectAndTriggerRecoveryIfNeededAsync(RecoveryContext context, CancellationToken cancellationToken = default);
    }
}
