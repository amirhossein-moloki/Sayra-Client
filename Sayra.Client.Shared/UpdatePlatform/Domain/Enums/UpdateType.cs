using System;

namespace Sayra.Client.Shared.UpdatePlatform.Domain.Enums
{
    /// <summary>
    /// Specifies the architectural class or urgency classification of the update package.
    /// </summary>
    public enum UpdateType
    {
        /// <summary>
        /// A comprehensive package containing all files required for a clean installation.
        /// </summary>
        Full,

        /// <summary>
        /// A differential package containing binary delta offsets to minimize bandwidth.
        /// </summary>
        Delta,

        /// <summary>
        /// A minor package resolving highly specific functional anomalies.
        /// </summary>
        Hotfix,

        /// <summary>
        /// A critical update resolving major security vulnerabilities.
        /// </summary>
        Security
    }
}
