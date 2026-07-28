using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Shared.UpdatePlatform.Application.Interfaces;
using Sayra.Client.Shared.UpdatePlatform.Application.Validation;
using Sayra.Client.Shared.UpdatePlatform.Domain.Enums;
using Sayra.Client.Shared.UpdatePlatform.Domain.Exceptions;
using Sayra.Client.Shared.UpdatePlatform.Domain.Models;

namespace Sayra.Client.Shared.UpdatePlatform.Application.Services
{
    /// <summary>
    /// Coordinates high-rigor structural, logical, manifest, and dependency validation of update packages.
    /// </summary>
    public class PackageValidator : IPackageValidator
    {
        private readonly IVersionValidator _versionValidator;

        /// <summary>
        /// Initializes a new instance of the <see cref="PackageValidator"/> class.
        /// </summary>
        public PackageValidator(IVersionValidator versionValidator)
        {
            _versionValidator = versionValidator ?? throw new ArgumentNullException(nameof(versionValidator));
        }

        /// <inheritdoc />
        public Task ValidateManifestAsync(UpdateManifest manifest, CancellationToken cancellationToken = default)
        {
            if (manifest == null)
            {
                throw new InvalidManifestException("Manifest is null.");
            }

            cancellationToken.ThrowIfCancellationRequested();

            if (manifest.Id == Guid.Empty)
            {
                throw new InvalidManifestException("Manifest Id cannot be empty.");
            }

            if (string.IsNullOrWhiteSpace(manifest.Version))
            {
                throw new InvalidManifestException("Manifest version is required.");
            }

            if (!_versionValidator.IsValid(manifest.Version))
            {
                throw new InvalidManifestException($"Manifest version '{manifest.Version}' is not a valid SemVer 2.0.0 string.");
            }

            if (string.IsNullOrWhiteSpace(manifest.ProductName))
            {
                throw new InvalidManifestException("Manifest Product Name is required.");
            }

            if (manifest.ReleaseDate == default)
            {
                throw new InvalidManifestException("Manifest Release Date is invalid.");
            }

            if (!string.IsNullOrWhiteSpace(manifest.MinimumClientVersion) && !_versionValidator.IsValid(manifest.MinimumClientVersion))
            {
                throw new InvalidManifestException($"Minimum client version '{manifest.MinimumClientVersion}' is not a valid SemVer 2.0.0 string.");
            }

            if (manifest.PackageType == PackageType.DeltaPackage)
            {
                if (string.IsNullOrWhiteSpace(manifest.RequiredVersion))
                {
                    throw new InvalidManifestException("Delta package manifest requires a 'RequiredVersion' specification.");
                }

                if (!_versionValidator.IsValid(manifest.RequiredVersion))
                {
                    throw new InvalidManifestException($"Delta required version '{manifest.RequiredVersion}' is not a valid SemVer 2.0.0 string.");
                }
            }

            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task ValidateChunksAsync(IReadOnlyList<ChunkMetadata> chunks, CancellationToken cancellationToken = default)
        {
            if (chunks == null || chunks.Count == 0)
            {
                throw new InvalidPackageException("Package contains no chunks.");
            }

            cancellationToken.ThrowIfCancellationRequested();

            long expectedOffset = 0;
            for (int i = 0; i < chunks.Count; i++)
            {
                var chunk = chunks[i];

                if (chunk.Index != i)
                {
                    throw new InvalidPackageException($"Chunk index mismatch. Expected index {i}, but got {chunk.Index}.");
                }

                if (chunk.SizeBytes <= 0)
                {
                    throw new InvalidPackageException($"Chunk at index {i} has invalid size: {chunk.SizeBytes} bytes.");
                }

                if (string.IsNullOrWhiteSpace(chunk.Sha256Checksum))
                {
                    throw new InvalidPackageException($"Chunk at index {i} is missing SHA-256 checksum.");
                }

                if (chunk.Offset < 0)
                {
                    throw new InvalidPackageException($"Chunk at index {i} has negative offset: {chunk.Offset}.");
                }

                expectedOffset = chunk.Offset + chunk.SizeBytes;
            }

            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task ValidateStructureAsync(PackageHeader header, UpdateManifest manifest, IReadOnlyList<ChunkMetadata> chunks, CancellationToken cancellationToken = default)
        {
            if (header == null) throw new InvalidPackageException("Package header is null.");
            if (manifest == null) throw new InvalidPackageException("Manifest is null.");
            if (chunks == null) throw new InvalidPackageException("Chunks list is null.");

            cancellationToken.ThrowIfCancellationRequested();

            // Validate header package ID matches manifest ID
            if (header.PackageId != manifest.Id)
            {
                throw new InvalidPackageException($"Package ID in header ({header.PackageId}) does not match manifest ID ({manifest.Id}).");
            }

            // Validate header version matches manifest version
            if (header.Version != manifest.Version)
            {
                throw new InvalidPackageException($"Package version in header ({header.Version}) does not match manifest version ({manifest.Version}).");
            }

            // Validate TotalSizeBytes matches total size of chunks
            long totalChunkSize = chunks.Sum(c => c.SizeBytes);
            if (header.TotalSizeBytes != totalChunkSize)
            {
                throw new InvalidPackageException($"Total size in header ({header.TotalSizeBytes}) does not match cumulative chunk size ({totalChunkSize}).");
            }

            return Task.CompletedTask;
        }
    }
}
