using System;

namespace Sayra.Client.Shared.UpdatePlatform.Application.Validation
{
    /// <summary>
    /// Provides verification validation over semantic version (SemVer 2.0.0) formatting rules.
    /// </summary>
    public interface IVersionValidator
    {
        /// <summary>
        /// Validates whether the given version string adheres to SemVer rules.
        /// </summary>
        /// <param name="version">The version string to validate.</param>
        /// <returns>True if valid; otherwise, false.</returns>
        bool IsValid(string version);

        /// <summary>
        /// Validates the version string and throws an exception on failure.
        /// </summary>
        /// <param name="version">The version string to validate.</param>
        void Validate(string version);
    }
}
