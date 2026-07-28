using System;
using Sayra.Client.Shared.UpdatePlatform.Domain.Enums;

namespace Sayra.Client.Shared.UpdatePlatform.Domain.Models
{
    /// <summary>
    /// Represents the immutable metadata and release options of an update payload.
    /// </summary>
    public class UpdateManifest
    {
        /// <summary>
        /// Gets or sets the unique identifier of the update manifest.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Gets or sets the semantic version of this release.
        /// </summary>
        public string Version { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the target product name.
        /// </summary>
        public string ProductName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the description or release notes of the update.
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the packaging classification.
        /// </summary>
        public PackageType PackageType { get; set; }

        /// <summary>
        /// Gets or sets the update classification (Full, Delta, Hotfix, etc.).
        /// </summary>
        public UpdateType UpdateType { get; set; }

        /// <summary>
        /// Gets or sets the version required to run this patch (relevant for delta updates).
        /// </summary>
        public string RequiredVersion { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the absolute minimum version of the client permitted to consume this update.
        /// </summary>
        public string MinimumClientVersion { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the release timestamp.
        /// </summary>
        public DateTime ReleaseDate { get; set; }

        /// <summary>
        /// Gets or sets the priority/urgency of this update.
        /// </summary>
        public UpdatePriority Priority { get; set; }

        /// <summary>
        /// Gets or sets the release channel of this update.
        /// </summary>
        public UpdateChannel Channel { get; set; }

        /// <summary>
        /// Gets or sets the signature or metadata for verification validation.
        /// </summary>
        public string SignatureMetadata { get; set; } = string.Empty;
    }
}
