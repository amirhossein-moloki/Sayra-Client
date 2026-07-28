using System;
using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Shared.UpdatePlatform.Domain.Models;

namespace Sayra.Client.Shared.UpdatePlatform.Application.Interfaces
{
    /// <summary>
    /// Represents a service responsible for validating the cryptographic authenticity and integrity of downloaded update packages and manifests.
    /// </summary>
    public interface IPackageVerifier
    {
        /// <summary>
        /// Cryptographically verifies the digital signature and SHA-256 integrity hash of a package.
        /// </summary>
        /// <param name="package">The downloaded update package metadata.</param>
        /// <param name="signature">The expected digital signature string.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>True if the package is verified and authentic; otherwise, false.</returns>
        Task<bool> VerifyAsync(UpdatePackage package, string signature, CancellationToken cancellationToken = default);

        /// <summary>
        /// Cryptographically verifies the digital signature of the update manifest.
        /// </summary>
        Task<bool> VerifyManifestSignatureAsync(UpdateManifest manifest, CancellationToken cancellationToken = default);

        /// <summary>
        /// Verifies the full package file integrity on disk (including block structure and chunk hashes).
        /// </summary>
        Task<bool> VerifyPackageIntegrityAsync(string packagePath, UpdatePackage packageMetadata, CancellationToken cancellationToken = default);

        /// <summary>
        /// Verifies the digital signature of a specific file.
        /// </summary>
        Task<bool> VerifyFileSignatureAsync(string filePath, string expectedSignature, CancellationToken cancellationToken = default);
    }
}
