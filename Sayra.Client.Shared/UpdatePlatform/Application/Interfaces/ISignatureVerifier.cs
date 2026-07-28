using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Sayra.Client.Shared.UpdatePlatform.Application.Interfaces
{
    /// <summary>
    /// Core cryptographic signature verification engine utilizing RSA/ECDsa.
    /// </summary>
    public interface ISignatureVerifier
    {
        /// <summary>
        /// Verifies a digital signature over a block of bytes using a public key.
        /// </summary>
        /// <param name="data">The raw data bytes.</param>
        /// <param name="signature">The digital signature (Base64 or Hex representation).</param>
        /// <param name="publicKeyPem">The public key in PEM format.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>True if the signature is valid; otherwise, false.</returns>
        Task<bool> VerifySignatureAsync(byte[] data, string signature, string publicKeyPem, CancellationToken cancellationToken = default);

        /// <summary>
        /// Verifies a digital signature over an entire stream using a public key, without loading the stream completely into memory.
        /// </summary>
        /// <param name="dataStream">The stream containing data to verify.</param>
        /// <param name="signature">The digital signature (Base64 or Hex representation).</param>
        /// <param name="publicKeyPem">The public key in PEM format.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>True if the signature is valid; otherwise, false.</returns>
        Task<bool> VerifyStreamSignatureAsync(Stream dataStream, string signature, string publicKeyPem, CancellationToken cancellationToken = default);
    }
}
