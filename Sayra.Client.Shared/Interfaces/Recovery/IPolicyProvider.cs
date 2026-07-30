using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Shared.Models.Recovery.Policies;

namespace Sayra.Client.Shared.Interfaces.Recovery
{
    /// <summary>
    /// Contract for dynamic administrative access, query, and configuration of subsystem-level recovery policies.
    /// </summary>
    public interface IPolicyProvider
    {
        /// <summary>
        /// Retrieves the recovery policy active for the specified subsystem.
        /// If not explicitly configured, a safe default policy is returned.
        /// </summary>
        Task<RecoveryPolicy> GetPolicyAsync(string subsystemName, CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves all registered subsystem-level recovery policies.
        /// </summary>
        Task<IReadOnlyList<RecoveryPolicy>> GetAllPoliciesAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Saves or updates the active recovery policy for a specific subsystem.
        /// </summary>
        Task SavePolicyAsync(RecoveryPolicy policy, CancellationToken cancellationToken = default);
    }
}
