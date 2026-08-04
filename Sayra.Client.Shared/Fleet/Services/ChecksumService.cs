using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace Sayra.Client.Shared.Fleet.Services
{
    /// <summary>
    /// Core interface for high-performance file hashing and integrity verification.
    /// </summary>
    public interface IChecksumService
    {
        /// <summary>
        /// Computes the cryptographic checksum hash for a file.
        /// </summary>
        Task<string> CalculateHashAsync(string filePath, string algorithm = "SHA256", CancellationToken ct = default);

        /// <summary>
        /// Computes the cryptographic checksum hash for a byte chunk.
        /// </summary>
        string CalculateChunkHash(byte[] buffer, int offset, int count, string algorithm = "SHA256");

        /// <summary>
        /// Verifies a file against a target hash.
        /// </summary>
        Task<bool> VerifyFileHashAsync(string filePath, string expectedHash, string algorithm = "SHA256", CancellationToken ct = default);
    }

    /// <summary>
    /// Enterprise high-performance checksum calculator supporting stream-based SHA256 and SHA512 validation.
    /// </summary>
    public class ChecksumService : IChecksumService
    {
        /// <summary>
        /// Calculates the checksum of a file asynchronously using streaming to ensure low memory footprint.
        /// </summary>
        public async Task<string> CalculateHashAsync(string filePath, string algorithm = "SHA256", CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("File path cannot be null or empty.", nameof(filePath));

            if (!File.Exists(filePath))
                throw new FileNotFoundException($"File not found for hashing: {filePath}", filePath);

            using var hashAlgorithm = CreateHashAlgorithm(algorithm);
            using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 8192, useAsync: true);

            byte[] buffer = new byte[8192];
            int bytesRead;

            while ((bytesRead = await fileStream.ReadAsync(buffer, 0, buffer.Length, ct).ConfigureAwait(false)) > 0)
            {
                hashAlgorithm.TransformBlock(buffer, 0, bytesRead, null, 0);
            }

            hashAlgorithm.TransformFinalBlock(Array.Empty<byte>(), 0, 0);

            byte[] hashBytes = hashAlgorithm.Hash ?? throw new InvalidOperationException("Hash computation failed.");
            return Convert.ToHexString(hashBytes).ToLowerInvariant();
        }

        /// <summary>
        /// Synchronously calculates the hash of an in-memory byte chunk.
        /// </summary>
        public string CalculateChunkHash(byte[] buffer, int offset, int count, string algorithm = "SHA256")
        {
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));

            using var hashAlgorithm = CreateHashAlgorithm(algorithm);
            byte[] hashBytes = hashAlgorithm.ComputeHash(buffer, offset, count);
            return Convert.ToHexString(hashBytes).ToLowerInvariant();
        }

        /// <summary>
        /// Verifies if a file matches an expected hash.
        /// </summary>
        public async Task<bool> VerifyFileHashAsync(string filePath, string expectedHash, string algorithm = "SHA256", CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(expectedHash)) return false;

            try
            {
                string calculatedHash = await CalculateHashAsync(filePath, algorithm, ct).ConfigureAwait(false);
                return string.Equals(calculatedHash, expectedHash, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private static HashAlgorithm CreateHashAlgorithm(string algorithm)
        {
            if (string.Equals(algorithm, "SHA512", StringComparison.OrdinalIgnoreCase))
            {
                return SHA512.Create();
            }
            return SHA256.Create();
        }
    }
}
