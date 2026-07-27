using System;

namespace Sayra.Client.Shared.UpdatePlatform.Domain.Enums
{
    /// <summary>
    /// Specifies the release stream channel of the updates.
    /// </summary>
    public enum UpdateChannel
    {
        /// <summary>
        /// Fully vetted stable release.
        /// </summary>
        Stable,

        /// <summary>
        /// Verified beta pre-releases.
        /// </summary>
        Beta,

        /// <summary>
        /// Internal testing/staging releases.
        /// </summary>
        Internal
    }
}
