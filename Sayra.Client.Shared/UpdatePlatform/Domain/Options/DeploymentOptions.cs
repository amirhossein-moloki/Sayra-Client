using System;
using Sayra.Client.Shared.UpdatePlatform.Domain.Enums;

namespace Sayra.Client.Shared.UpdatePlatform.Domain.Options
{
    /// <summary>
    /// Configuration options for deployment policies and rings.
    /// </summary>
    public class DeploymentOptions
    {
        public string CurrentVersion { get; set; } = "1.0.0";
        public DeploymentRing Ring { get; set; } = DeploymentRing.Production;
        public UpdateChannel Channel { get; set; } = UpdateChannel.Stable;
        public bool AutoUpdate { get; set; } = true;
        public bool BypassActiveUserSession { get; set; }
        public bool ForceImmediate { get; set; }
    }
}
