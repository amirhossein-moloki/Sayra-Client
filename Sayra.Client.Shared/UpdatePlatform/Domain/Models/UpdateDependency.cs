using System;

namespace Sayra.Client.Shared.UpdatePlatform.Domain.Models
{
    /// <summary>
    /// Represents a technical dependency required by an update package.
    /// </summary>
    public class UpdateDependency
    {
        /// <summary>
        /// Gets or sets the name of the dependent module or framework.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the minimum semantic version required.
        /// </summary>
        public string MinimumVersion { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets a value indicating whether this dependency is strictly required.
        /// </summary>
        public bool Required { get; set; }
    }
}
