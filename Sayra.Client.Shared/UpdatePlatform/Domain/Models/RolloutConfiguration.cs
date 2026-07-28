using System;
using System.Collections.Generic;
using Sayra.Client.Shared.UpdatePlatform.Domain.Enums;

namespace Sayra.Client.Shared.UpdatePlatform.Domain.Models
{
    /// <summary>
    /// Represents the staged rollout configuration for an update deployment.
    /// </summary>
    public class RolloutConfiguration
    {
        /// <summary>
        /// Globally unique identifier of the deployment/rollout campaign.
        /// </summary>
        public Guid CampaignId { get; set; } = Guid.NewGuid();

        /// <summary>
        /// The current staged rollout percentage (0 to 100).
        /// </summary>
        public int RolloutPercentage { get; set; } = 100;

        /// <summary>
        /// Indicates if the rollout is currently paused.
        /// </summary>
        public bool IsPaused { get; set; }

        /// <summary>
        /// Indicates if the rollout has been cancelled.
        /// </summary>
        public bool IsCancelled { get; set; }

        /// <summary>
        /// Specific workstation identifiers excluded from the rollout.
        /// </summary>
        public List<string> ExcludedDeviceIds { get; set; } = new();

        /// <summary>
        /// Progressive rings targetable by this rollout configuration.
        /// </summary>
        public List<DeploymentRing> TargetRings { get; set; } = new() { DeploymentRing.Production };
    }
}
