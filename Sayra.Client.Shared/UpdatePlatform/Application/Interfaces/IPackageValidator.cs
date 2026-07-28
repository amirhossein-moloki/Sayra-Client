using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Shared.UpdatePlatform.Domain.Models;

namespace Sayra.Client.Shared.UpdatePlatform.Application.Interfaces
{
    /// <summary>
    /// Coordinates structural and logical validation of package manifests, chunk collections, dependencies, and file structures.
    /// </summary>
    public interface IPackageValidator
    {
        /// <summary>
        /// Validates a package manifest, checking required fields, versions, and format types.
        /// </summary>
        Task ValidateManifestAsync(UpdateManifest manifest, CancellationToken cancellationToken = default);

        /// <summary>
        /// Validates an array of chunk metadata, checking sizes, offsets, and checksum integrity formatting.
        /// </summary>
        Task ValidateChunksAsync(IReadOnlyList<ChunkMetadata> chunks, CancellationToken cancellationToken = default);

        /// <summary>
        /// Validates overall consistency between the package header, manifest, and chunk definitions.
        /// </summary>
        Task ValidateStructureAsync(PackageHeader header, UpdateManifest manifest, IReadOnlyList<ChunkMetadata> chunks, CancellationToken cancellationToken = default);
    }
}
