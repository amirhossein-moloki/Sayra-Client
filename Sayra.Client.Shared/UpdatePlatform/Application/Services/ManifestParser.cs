using System;
using System.Text.Json;
using Sayra.Client.Shared.UpdatePlatform.Application.Interfaces;
using Sayra.Client.Shared.UpdatePlatform.Domain.Exceptions;
using Sayra.Client.Shared.UpdatePlatform.Domain.Models;

namespace Sayra.Client.Shared.UpdatePlatform.Application.Services
{
    /// <summary>
    /// Implements safe, version-tolerant JSON manifest parsing and serialization.
    /// </summary>
    public class ManifestParser : IManifestParser
    {
        private static readonly JsonSerializerOptions Options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = true
        };

        /// <inheritdoc />
        public UpdateManifest Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new InvalidManifestException("Manifest JSON cannot be null or empty.");
            }

            try
            {
                var manifest = JsonSerializer.Deserialize<UpdateManifest>(json, Options);
                if (manifest == null)
                {
                    throw new InvalidManifestException("Deserialized manifest is null.");
                }

                return manifest;
            }
            catch (JsonException ex)
            {
                throw new InvalidManifestException($"Malformed manifest JSON: {ex.Message}", ex);
            }
            catch (Exception ex)
            {
                throw new InvalidManifestException($"Failed to parse manifest: {ex.Message}", ex);
            }
        }

        /// <inheritdoc />
        public string Serialize(UpdateManifest manifest)
        {
            if (manifest == null)
            {
                throw new ArgumentNullException(nameof(manifest));
            }

            try
            {
                return JsonSerializer.Serialize(manifest, Options);
            }
            catch (Exception ex)
            {
                throw new InvalidManifestException($"Failed to serialize manifest: {ex.Message}", ex);
            }
        }
    }
}
