using System;
using Sayra.Client.Shared.UpdatePlatform.Domain.Enums;

namespace Sayra.Client.Shared.UpdatePlatform.Domain.Models
{
    /// <summary>
    /// Represents the embedded JSON header detailing package metadata.
    /// </summary>
    public class PackageHeader
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
        /// Gets or sets the target hardware architecture.
        /// </summary>
        public SystemArchitecture TargetArchitecture { get; set; }

        /// <summary>
        /// Gets or sets the package total byte size.
        /// </summary>
        public long TotalSizeBytes { get; set; }

        /// <summary>
        /// Gets or sets the creation timestamp.
        /// </summary>
        public DateTime CreatedAt { get; set; }
    }
}
