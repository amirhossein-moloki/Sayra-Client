using System;
using Sayra.Client.Shared.UpdatePlatform.Domain.Exceptions;
using Sayra.Client.Shared.UpdatePlatform.Domain.Models;

namespace Sayra.Client.Shared.UpdatePlatform.Application.Validation
{
    /// <summary>
    /// Validates update manifest properties and business logic parameters.
    /// </summary>
    public class ManifestValidator : IManifestValidator
    {
        private readonly IVersionValidator _versionValidator;

        /// <summary>
        /// Initializes a new instance of the <see cref="ManifestValidator"/> class.
        /// </summary>
        /// <param name="versionValidator">The version validator.</param>
        public ManifestValidator(IVersionValidator versionValidator)
        {
            _versionValidator = versionValidator ?? throw new ArgumentNullException(nameof(versionValidator));
        }

        /// <inheritdoc />
        public void Validate(UpdateManifest manifest)
        {
            if (manifest == null)
            {
                throw new UpdateValidationException("Update manifest cannot be null.");
            }

            if (manifest.Id == Guid.Empty)
            {
                throw new UpdateValidationException("Manifest unique ID cannot be Guid.Empty.");
            }

            if (string.IsNullOrWhiteSpace(manifest.ProductName))
            {
                throw new UpdateValidationException("Manifest ProductName is required and cannot be empty.");
            }

            try
            {
                _versionValidator.Validate(manifest.Version);
            }
            catch (UpdateValidationException ex)
            {
                throw new UpdateValidationException($"Manifest version is invalid: {ex.Message}", ex);
            }

            try
            {
                _versionValidator.Validate(manifest.MinimumClientVersion);
            }
            catch (UpdateValidationException ex)
            {
                throw new UpdateValidationException($"Manifest MinimumClientVersion is invalid: {ex.Message}", ex);
            }

            // RequiredVersion is only required for Delta updates or if specifically supplied
            if (manifest.PackageType == Domain.Enums.PackageType.DeltaPackage || !string.IsNullOrWhiteSpace(manifest.RequiredVersion))
            {
                try
                {
                    _versionValidator.Validate(manifest.RequiredVersion);
                }
                catch (UpdateValidationException ex)
                {
                    throw new UpdateValidationException($"Manifest RequiredVersion is invalid: {ex.Message}", ex);
                }
            }

            if (manifest.ReleaseDate == default)
            {
                throw new UpdateValidationException("Manifest ReleaseDate must be a valid non-default date.");
            }
        }
    }
}
