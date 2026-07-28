using System;

namespace Sayra.Client.Shared.UpdatePlatform.Domain.Enums
{
    /// <summary>
    /// Governs execution urgency and scheduling windows of the update package.
    /// </summary>
    public enum UpdatePriority
    {
        /// <summary>
        /// Deferrable update scheduled during low workstation utilization.
        /// </summary>
        Low,

        /// <summary>
        /// Standard update applied during normal maintenance windows.
        /// </summary>
        Normal,

        /// <summary>
        /// Elevated update to be prioritised for installation.
        /// </summary>
        High,

        /// <summary>
        /// Critical update requiring immediate installation, overriding active sessions if configured.
        /// </summary>
        Critical
    }
}
