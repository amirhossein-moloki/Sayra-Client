using System;
using Sayra.Client.Shared.UpdatePlatform.Domain.Models;

namespace Sayra.Client.Shared.UpdatePlatform.Application.Validation
{
    /// <summary>
    /// Provides verification validation for package prerequisites and system dependencies.
    /// </summary>
    public interface IDependencyValidator
    {
        /// <summary>
        /// Validates package dependency properties.
        /// </summary>
        /// <param name="dependency">The dependency to validate.</param>
        void Validate(UpdateDependency dependency);
    }
}
