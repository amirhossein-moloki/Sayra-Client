using System;
using Sayra.Client.Shared.UpdatePlatform.Domain.Models;

namespace Sayra.Client.Shared.UpdatePlatform.Application.Validation
{
    /// <summary>
    /// Unified validation service aggregating all update validation rules.
    /// </summary>
    public interface IUpdateValidator
    {
        /// <summary>
        /// Validates update manifest properties.
        /// </summary>
        /// <param name="manifest">The update manifest to validate.</param>
        void ValidateManifest(UpdateManifest manifest);

        /// <summary>
        /// Validates whether the given version string adheres to SemVer rules.
        /// </summary>
        /// <param name="version">The version string to validate.</param>
        void ValidateVersion(string version);

        /// <summary>
        /// Validates package dependency properties.
        /// </summary>
        /// <param name="dependency">The dependency to validate.</param>
        void ValidateDependency(UpdateDependency dependency);
    }
}
