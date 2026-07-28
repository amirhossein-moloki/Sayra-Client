using System;
using Sayra.Client.Shared.UpdatePlatform.Domain.Enums;

namespace Sayra.Client.Shared.UpdatePlatform.Application.Dtos
{
    /// <summary>
    /// Data Transfer Object representing the update release manifest.
    /// </summary>
    public class UpdateManifestDto
    {
        /// <summary>
        /// Gets or sets the manifest unique ID.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Gets or sets the target semantic version code.
        /// </summary>
        public string Version { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the product name.
        /// </summary>
        public string ProductName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the description or release notes.
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the packing format.
        /// </summary>
        public PackageType PackageType { get; set; }

        /// <summary>
        /// Gets or sets the class categorization of the update.
        /// </summary>
        public UpdateType UpdateType { get; set; }

        /// <summary>
        /// Gets or sets the prerequisite baseline version required.
        /// </summary>
        public string RequiredVersion { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the absolute minimum version allowed.
        /// </summary>
        public string MinimumClientVersion { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the release timestamp.
        /// </summary>
        public DateTime ReleaseDate { get; set; }

        /// <summary>
        /// Gets or sets the priority level.
        /// </summary>
        public UpdatePriority Priority { get; set; }

        /// <summary>
        /// Gets or sets the release stream channel.
        /// </summary>
        public UpdateChannel Channel { get; set; }

        /// <summary>
        /// Gets or sets the cryptographic validation signature.
        /// </summary>
        public string SignatureMetadata { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets whether this update is a forced upgrade that overrides standard scheduling and maintenance windows.
        /// </summary>
        public bool IsForcedUpgrade { get; set; }
    }
}
