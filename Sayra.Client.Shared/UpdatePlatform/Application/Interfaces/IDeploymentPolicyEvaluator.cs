using System;
using Sayra.Client.Shared.UpdatePlatform.Domain.Models;

namespace Sayra.Client.Shared.UpdatePlatform.Application.Interfaces
{
    /// <summary>
    /// Contract for evaluating update manifests against configured deployment policies.
    /// </summary>
    public interface IDeploymentPolicyEvaluator
    {
        /// <summary>
        /// Evaluates whether an update is permitted under the current deployment policy rules.
        /// </summary>
        /// <param name="manifest">The update manifest to evaluate.</param>
        /// <param name="policy">The applied deployment policy.</param>
        /// <returns>True if permitted; false otherwise.</returns>
        bool EvaluatePolicy(UpdateManifest manifest, DeploymentPolicy policy);

        /// <summary>
        /// Checks if an update must be treated as a forced update.
        /// </summary>
        bool IsForcedUpdate(UpdateManifest manifest, DeploymentPolicy policy);
    }
}
