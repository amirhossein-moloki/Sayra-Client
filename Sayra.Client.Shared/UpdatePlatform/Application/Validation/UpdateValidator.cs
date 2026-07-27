using System;
using Sayra.Client.Shared.UpdatePlatform.Domain.Models;

namespace Sayra.Client.Shared.UpdatePlatform.Application.Validation
{
    /// <summary>
    /// Aggregated validator coordinating and executing individual update component validations.
    /// </summary>
    public class UpdateValidator : IUpdateValidator
    {
        private readonly IManifestValidator _manifestValidator;
        private readonly IVersionValidator _versionValidator;
        private readonly IDependencyValidator _dependencyValidator;

        /// <summary>
        /// Initializes a new instance of the <see cref="UpdateValidator"/> class.
        /// </summary>
        /// <param name="manifestValidator">The manifest validator.</param>
        /// <param name="versionValidator">The version validator.</param>
        /// <param name="dependencyValidator">The dependency validator.</param>
        public UpdateValidator(
            IManifestValidator manifestValidator,
            IVersionValidator versionValidator,
            IDependencyValidator dependencyValidator)
        {
            _manifestValidator = manifestValidator ?? throw new ArgumentNullException(nameof(manifestValidator));
            _versionValidator = versionValidator ?? throw new ArgumentNullException(nameof(versionValidator));
            _dependencyValidator = dependencyValidator ?? throw new ArgumentNullException(nameof(dependencyValidator));
        }

        /// <inheritdoc />
        public void ValidateManifest(UpdateManifest manifest)
        {
            _manifestValidator.Validate(manifest);
        }

        /// <inheritdoc />
        public void ValidateVersion(string version)
        {
            _versionValidator.Validate(version);
        }

        /// <inheritdoc />
        public void ValidateDependency(UpdateDependency dependency)
        {
            _dependencyValidator.Validate(dependency);
        }
    }
}
