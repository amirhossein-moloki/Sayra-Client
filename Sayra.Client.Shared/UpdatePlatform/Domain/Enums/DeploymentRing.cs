using System;

namespace Sayra.Client.Shared.UpdatePlatform.Domain.Enums
{
    /// <summary>
    /// Represents the workstation's progressive deployment target level.
    /// </summary>
    public enum DeploymentRing
    {
        /// <summary>
        /// Highly unstable development and testing workstations.
        /// </summary>
        Development,

        /// <summary>
        /// Initial minor fleet deployment targeting early telemetry validation.
        /// </summary>
        Canary,

        /// <summary>
        /// Intermediate pilot branches or specific test sites.
        /// </summary>
        Pilot,

        /// <summary>
        /// General fleet production level.
        /// </summary>
        Production
    }
}
