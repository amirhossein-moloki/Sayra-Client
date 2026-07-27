using System;
using System.Collections.Generic;
using Sayra.Client.Shared.UpdatePlatform.Domain.Enums;

namespace Sayra.Client.Shared.UpdatePlatform.Domain.Models
{
    /// <summary>
    /// Represents the secure update package payload metadata.
    /// </summary>
    public class UpdatePackage
    {
        /// <summary>
        /// Gets or sets the unique package identifier.
        /// </summary>
        public Guid PackageId { get; set; }

        /// <summary>
        /// Gets or sets the target version of this package.
        /// </summary>
        public string Version { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the package total byte size.
        /// </summary>
        public long Size { get; set; }

        /// <summary>
        /// Gets or sets the SHA-256 hash of the update package archive.
        /// </summary>
        public string Hash { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the package type.
        /// </summary>
        public PackageType PackageType { get; set; }

        /// <summary>
        /// Gets or sets the UTC timestamp when the package was generated.
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Gets or sets the prerequisites list for this update.
        /// </summary>
        public List<UpdateDependency> Dependencies { get; set; } = new List<UpdateDependency>();
    }
}
