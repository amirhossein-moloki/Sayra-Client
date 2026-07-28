using System;
using Sayra.Client.Shared.UpdatePlatform.Domain.Models;

namespace Sayra.Client.Shared.UpdatePlatform.Application.Interfaces
{
    /// <summary>
    /// Represents a service that safely parses and serializes update manifests.
    /// </summary>
    public interface IManifestParser
    {
        /// <summary>
        /// Parses a JSON representation of an update manifest.
        /// </summary>
        /// <param name="json">The manifest JSON string.</param>
        /// <returns>A strongly-typed <see cref="UpdateManifest"/>.</returns>
        /// <exception cref="Sayra.Client.Shared.UpdatePlatform.Domain.Exceptions.InvalidManifestException">Thrown if parsing fails or fields are invalid.</exception>
        UpdateManifest Parse(string json);

        /// <summary>
        /// Serializes an update manifest into a JSON string.
        /// </summary>
        /// <param name="manifest">The update manifest to serialize.</param>
        /// <returns>A JSON string representation.</returns>
        string Serialize(UpdateManifest manifest);
    }
}
