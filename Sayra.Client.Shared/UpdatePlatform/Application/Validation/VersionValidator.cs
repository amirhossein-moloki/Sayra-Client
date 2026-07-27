using System;
using System.Text.RegularExpressions;
using Sayra.Client.Shared.UpdatePlatform.Domain.Exceptions;

namespace Sayra.Client.Shared.UpdatePlatform.Application.Validation
{
    /// <summary>
    /// Validates version formatting against the SemVer 2.0.0 specification.
    /// </summary>
    public class VersionValidator : IVersionValidator
    {
        private static readonly Regex SemVerRegex = new Regex(
            @"^([0-9]+)\.([0-9]+)\.([0-9]+)(?:-([0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*))?(?:\+([0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*))?$",
            RegexOptions.Compiled);

        /// <inheritdoc />
        public bool IsValid(string version)
        {
            if (string.IsNullOrWhiteSpace(version))
                return false;

            return SemVerRegex.IsMatch(version.Trim());
        }

        /// <inheritdoc />
        public void Validate(string version)
        {
            if (string.IsNullOrWhiteSpace(version))
            {
                throw new UpdateValidationException("Version string cannot be null or empty.");
            }

            if (!IsValid(version))
            {
                throw new UpdateValidationException($"Version string '{version}' does not adhere to Semantic Versioning (SemVer 2.0.0) requirements.");
            }
        }
    }
}
