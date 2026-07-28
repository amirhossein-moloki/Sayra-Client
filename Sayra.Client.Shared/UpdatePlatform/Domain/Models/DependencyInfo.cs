using System;

namespace Sayra.Client.Shared.UpdatePlatform.Domain.Models
{
    /// <summary>
    /// Represents dependency requirements for package validation.
    /// </summary>
    public class DependencyInfo
    {
        /// <summary>
        /// Gets or sets the name of the dependent target.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the target version criteria or range (e.g. ">=2.0.0").
        /// </summary>
        public string VersionRange { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets a value indicating whether this dependency is optional.
        /// </summary>
        public bool IsOptional { get; set; }

        /// <summary>
        /// Converts to the standard <see cref="UpdateDependency"/> representation.
        /// </summary>
        public UpdateDependency ToUpdateDependency()
        {
            return new UpdateDependency
            {
                Name = Name,
                MinimumVersion = VersionRange.Replace(">=", "").Trim(),
                Required = !IsOptional
            };
        }

        /// <summary>
        /// Creates a <see cref="DependencyInfo"/> from an <see cref="UpdateDependency"/>.
        /// </summary>
        public static DependencyInfo FromUpdateDependency(UpdateDependency dependency)
        {
            return new DependencyInfo
            {
                Name = dependency.Name,
                VersionRange = $">={dependency.MinimumVersion}",
                IsOptional = !dependency.Required
            };
        }
    }
}
