using System;
using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Shared.UpdatePlatform.Domain.Models;

namespace Sayra.Client.Shared.UpdatePlatform.Application.Interfaces
{
    /// <summary>
    /// Represents a service responsible for validating the cryptographic authenticity and integrity of downloaded update packages.
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
    }
}
