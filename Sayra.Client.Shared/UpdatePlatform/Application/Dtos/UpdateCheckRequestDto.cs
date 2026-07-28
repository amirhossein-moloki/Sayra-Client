using System;
using Sayra.Client.Shared.UpdatePlatform.Domain.Enums;

namespace Sayra.Client.Shared.UpdatePlatform.Application.Dtos
{
    /// <summary>
    /// Data Transfer Object for checking for new updates from the workstation.
    /// </summary>
    public class UpdateCheckRequestDto
    {
        /// <summary>
        /// Gets or sets the physical workstation unique ID.
        /// </summary>
        public string WorkstationId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the currently installed version code.
        /// </summary>
        public string CurrentVersion { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the release stream channel (Stable, Beta, etc.).
        /// </summary>
        public UpdateChannel Channel { get; set; }

        /// <summary>
        /// Gets or sets the target deployment ring for phased rollouts.
        /// </summary>
        public DeploymentRing DeploymentRing { get; set; }
    }
}
