using System;
using Sayra.Client.Shared.UpdatePlatform.Application.Interfaces;
using Sayra.Client.Shared.UpdatePlatform.Domain.Exceptions;
using Sayra.Client.Shared.UpdatePlatform.Domain.Models;

namespace Sayra.Client.Shared.UpdatePlatform.Application.Services
{
    /// <summary>
    /// Service for evaluating whether an update is permitted under configured deployment policies.
    /// </summary>
    public class DeploymentPolicyEvaluator : IDeploymentPolicyEvaluator
    {
        public bool EvaluatePolicy(UpdateManifest manifest, DeploymentPolicy policy)
        {
            if (manifest == null) throw new ArgumentNullException(nameof(manifest));
            if (policy == null) throw new ArgumentNullException(nameof(policy));

            // Check if update is deferred
            if (policy.DeferralDays > 0)
            {
                var eligibleDate = manifest.ReleaseDate.AddDays(policy.DeferralDays);
                if (DateTime.UtcNow < eligibleDate)
                {
                    throw new DeploymentPolicyException($"Update is deferred until {eligibleDate} (Deferral: {policy.DeferralDays} days).");
                }
            }

            // Check if manual approval is required
            if (policy.RequiresApproval && !policy.IsEmergency && !policy.IsForced)
            {
                // Requires manual approval, cannot auto-install or auto-apply
                return false;
            }

            return policy.IsAutomatic || policy.IsEmergency || policy.IsForced;
        }

        public bool IsForcedUpdate(UpdateManifest manifest, DeploymentPolicy policy)
        {
            if (manifest == null) throw new ArgumentNullException(nameof(manifest));
            if (policy == null) throw new ArgumentNullException(nameof(policy));

            return policy.IsForced || manifest.Priority == Domain.Enums.UpdatePriority.Critical || policy.IsEmergency;
        }
    }
}
