using System;
using Sayra.Client.Shared.UpdatePlatform.Domain.Enums;

namespace Sayra.Client.Shared.UpdatePlatform.Domain.Models
{
    /// <summary>
    /// Captures the deployment ring assignment for a specific client/workstation.
    /// </summary>
    public class DeploymentRingAssignment
    {
        /// <summary>
        /// Unique workstation identifier.
        /// </summary>
        public string WorkstationId { get; set; } = string.Empty;

        /// <summary>
        /// Assigned progressive deployment ring.
        /// </summary>
        public DeploymentRing Ring { get; set; } = DeploymentRing.Production;

        /// <summary>
        /// When this assignment was made.
        /// </summary>
        public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
    }
}
