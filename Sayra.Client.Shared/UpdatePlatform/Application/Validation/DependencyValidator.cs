using System;
using Sayra.Client.Shared.UpdatePlatform.Domain.Exceptions;
using Sayra.Client.Shared.UpdatePlatform.Domain.Models;

namespace Sayra.Client.Shared.UpdatePlatform.Application.Validation
{
    /// <summary>
    /// Validates package update dependencies.
    /// </summary>
    public class DependencyValidator : IDependencyValidator
    {
        private readonly IVersionValidator _versionValidator;

        /// <summary>
        /// Initializes a new instance of the <see cref="DependencyValidator"/> class.
        /// </summary>
        /// <param name="versionValidator">The version validator.</param>
        public DependencyValidator(IVersionValidator versionValidator)
        {
            _versionValidator = versionValidator ?? throw new ArgumentNullException(nameof(versionValidator));
        }

        /// <inheritdoc />
        public void Validate(UpdateDependency dependency)
        {
            if (dependency == null)
            {
                throw new UpdateValidationException("Dependency model cannot be null.");
            }

            if (string.IsNullOrWhiteSpace(dependency.Name))
            {
                throw new UpdateValidationException("Dependency Name is required and cannot be empty.");
            }

            try
            {
                _versionValidator.Validate(dependency.MinimumVersion);
            }
            catch (UpdateValidationException ex)
            {
                throw new UpdateValidationException($"Dependency '{dependency.Name}' has an invalid minimum version: {ex.Message}", ex);
            }
        }
    }
}
