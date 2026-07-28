using System;
using Sayra.Client.Shared.UpdatePlatform.Domain.Models;

namespace Sayra.Client.Shared.UpdatePlatform.Application.Validation
{
    /// <summary>
    /// Provides verification validation over update manifests.
    /// </summary>
    public interface IManifestValidator
    {
        /// <summary>
        /// Validates update manifest properties.
        /// </summary>
        /// <param name="manifest">The update manifest to validate.</param>
        void Validate(UpdateManifest manifest);
    }
}
