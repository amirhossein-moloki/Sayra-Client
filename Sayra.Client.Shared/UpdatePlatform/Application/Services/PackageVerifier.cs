using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Shared.UpdatePlatform.Application.Interfaces;
using Sayra.Client.Shared.UpdatePlatform.Domain.Exceptions;
using Sayra.Client.Shared.UpdatePlatform.Domain.Models;

namespace Sayra.Client.Shared.UpdatePlatform.Application.Services
{
    /// <summary>
    /// Implements high-rigor cryptographic and structural verification of manifests,
    /// package signatures, and chunk-by-chunk SHA-256 hashes.
    /// </summary>
    public class PackageVerifier : IPackageVerifier
    {
        private readonly ISignatureVerifier _signatureVerifier;
        private readonly IPackageValidator _packageValidator;
        private string? _cachedPublicKey;

        /// <summary>
        /// Initializes a new instance of the <see cref="PackageVerifier"/> class.
        /// </summary>
        public PackageVerifier(ISignatureVerifier signatureVerifier, IPackageValidator packageValidator)
        {
            _signatureVerifier = signatureVerifier ?? throw new ArgumentNullException(nameof(signatureVerifier));
            _packageValidator = packageValidator ?? throw new ArgumentNullException(nameof(packageValidator));
        }

        /// <inheritdoc />
        public async Task<bool> VerifyAsync(UpdatePackage package, string signature, CancellationToken cancellationToken = default)
        {
            if (package == null) throw new ArgumentNullException(nameof(package));
            if (string.IsNullOrWhiteSpace(signature)) throw new InvalidSignatureException("Package signature cannot be empty.");

            cancellationToken.ThrowIfCancellationRequested();

            string publicKey = GetPublicKey();
            byte[] hashBytes = Encoding.UTF8.GetBytes(package.Hash);

            return await _signatureVerifier.VerifySignatureAsync(hashBytes, signature, publicKey, cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<bool> VerifyManifestSignatureAsync(UpdateManifest manifest, CancellationToken cancellationToken = default)
        {
            if (manifest == null) throw new ArgumentNullException(nameof(manifest));
            if (string.IsNullOrWhiteSpace(manifest.SignatureMetadata))
            {
                throw new InvalidSignatureException("Manifest signature metadata is missing or empty.");
            }

            cancellationToken.ThrowIfCancellationRequested();

            string publicKey = GetPublicKey();

            // Construct a canonical string of the manifest data to verify
            // For determinism, we use the fields in the manifest to create a verification payload
            string verificationPayload = $"{manifest.Id}:{manifest.Version}:{manifest.ProductName}:{manifest.PackageType}:{manifest.UpdateType}";
            byte[] payloadBytes = Encoding.UTF8.GetBytes(verificationPayload);

            return await _signatureVerifier.VerifySignatureAsync(payloadBytes, manifest.SignatureMetadata, publicKey, cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<bool> VerifyPackageIntegrityAsync(string packagePath, UpdatePackage packageMetadata, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(packagePath)) throw new ArgumentException("Package path cannot be empty.", nameof(packagePath));
            if (!File.Exists(packagePath)) throw new FileNotFoundException("Package file not found.", packagePath);

            cancellationToken.ThrowIfCancellationRequested();

            using (var reader = new PackageReader())
            {
                // 1. Open and parse package structure (Magic, Header, Manifest, Chunks, Footer)
                await reader.OpenAsync(packagePath, cancellationToken).ConfigureAwait(false);

                var header = await reader.ReadHeaderAsync(cancellationToken).ConfigureAwait(false);
                var manifest = await reader.ReadManifestAsync(cancellationToken).ConfigureAwait(false);
                var chunks = await reader.ReadChunksAsync(cancellationToken).ConfigureAwait(false);
                var footer = await reader.ReadFooterAsync(cancellationToken).ConfigureAwait(false);

                // 2. Structural & Field Validations
                await _packageValidator.ValidateManifestAsync(manifest, cancellationToken).ConfigureAwait(false);
                await _packageValidator.ValidateChunksAsync(chunks, cancellationToken).ConfigureAwait(false);
                await _packageValidator.ValidateStructureAsync(header, manifest, chunks, cancellationToken).ConfigureAwait(false);

                // 3. Chunk-level SHA-256 Checksums validation
                for (int i = 0; i < chunks.Count; i++)
                {
                    var chunk = chunks[i];
                    using (var chunkStream = await reader.GetChunkStreamAsync(i, cancellationToken).ConfigureAwait(false))
                    {
                        using (var sha256 = SHA256.Create())
                        {
                            byte[] buffer = new byte[8192];
                            int bytesRead;
                            while ((bytesRead = await chunkStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false)) > 0)
                            {
                                sha256.TransformBlock(buffer, 0, bytesRead, null, 0);
                            }
                            sha256.TransformFinalBlock(Array.Empty<byte>(), 0, 0);

                            byte[] chunkHashBytes = sha256.Hash ?? throw new PackageCorruptedException($"Failed to compute hash for chunk {i}.");
                            string computedHash = ConvertToHexString(chunkHashBytes);

                            if (!string.Equals(computedHash, chunk.Sha256Checksum, StringComparison.OrdinalIgnoreCase))
                            {
                                throw new PackageCorruptedException($"Chunk {i} integrity verification failed. Expected: {chunk.Sha256Checksum}, Got: {computedHash}");
                            }
                        }
                    }
                }

                // 4. Validate overall Digital Signature over the entire file preceding the trailer
                string publicKey = GetPublicKey();
                using (var fs = new FileStream(packagePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true))
                {
                    // Calculate start of footer (Length - 8 bytes for length and magic - footer length)
                    fs.Position = fs.Length - 8;
                    byte[] footerLenBytes = new byte[4];
                    int read = await fs.ReadAsync(footerLenBytes, 0, 4, cancellationToken).ConfigureAwait(false);
                    if (read < 4) throw new InvalidPackageException("Failed to read footer length during integrity check.");
                    int footerLen = BitConverter.ToInt32(footerLenBytes, 0);

                    long footerStartPos = fs.Length - 8 - footerLen;
                    if (footerStartPos <= 0) throw new InvalidPackageException("Corrupted footer start position.");

                    // Verify digital signature over the entire file block preceding the footer start position
                    var subStream = new BoundedFileStream(fs, footerStartPos);
                    bool isSignatureValid = await _signatureVerifier.VerifyStreamSignatureAsync(subStream, footer.Signature, publicKey, cancellationToken).ConfigureAwait(false);

                    if (!isSignatureValid)
                    {
                        throw new InvalidSignatureException("Package digital signature verification failed. The package envelope signature is invalid or tampered.");
                    }
                }
            }

            return true;
        }

        /// <inheritdoc />
        public async Task<bool> VerifyFileSignatureAsync(string filePath, string expectedSignature, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentException("File path cannot be empty.", nameof(filePath));
            if (!File.Exists(filePath)) throw new FileNotFoundException("Target file not found.", filePath);
            if (string.IsNullOrWhiteSpace(expectedSignature)) throw new InvalidSignatureException("Expected signature is empty.");

            cancellationToken.ThrowIfCancellationRequested();

            string publicKey = GetPublicKey();
            using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true))
            {
                return await _signatureVerifier.VerifyStreamSignatureAsync(fs, expectedSignature, publicKey, cancellationToken).ConfigureAwait(false);
            }
        }

        private string GetPublicKey()
        {
            if (_cachedPublicKey != null)
            {
                return _cachedPublicKey;
            }

            // Search for server_public.key in typical directories
            string[] pathsToTry = new[]
            {
                Path.Combine(AppContext.BaseDirectory, "server_public.key"),
                Path.Combine(Directory.GetCurrentDirectory(), "server_public.key"),
                Path.Combine(Directory.GetParent(Directory.GetCurrentDirectory())?.FullName ?? "", "server_public.key"),
                Path.Combine(Directory.GetParent(Directory.GetCurrentDirectory())?.Parent?.FullName ?? "", "server_public.key"),
                "server_public.key"
            };

            foreach (var path in pathsToTry)
            {
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                {
                    try
                    {
                        _cachedPublicKey = File.ReadAllText(path);
                        return _cachedPublicKey;
                    }
                    catch (Exception)
                    {
                        // Ignore and try next path
                    }
                }
            }

            // Fallback: Use the known RSA public key directly to guarantee it works in any environment
            const string FallbackKey = @"-----BEGIN PUBLIC KEY-----
MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEA4eruinhO6XBfUZUnpOwf
MILtJ93xkvRXHJanbIo66kosTC2tldJsE2OW4KwZ2NQ7QRf+UT770HStKDmBUwJ6
b2kHSAhxkbf7jhGJfgscnndNY2WGXCa7l022lhFg6IABJfpOy7Xl+/TszqtC7urG
FxFfmkRCg4uWm1Hd1czHT7moBkYXH4J9HBc5cGcGqQUc0rnm9hnckKdUic0uP7HW
wbkq8cQykV7f1eTY5RvnMB7MpwTz869vIWqrfrJNESx/9Z9nqxaGh7ND3nSeekmR
QrPuv+ZvR1UOdR0qylxZZIXgPK3NfOKyfR9AlHzDJJtg1i/qpoqd57BL7060FvC+
IwIDAQAB
-----END PUBLIC KEY-----";

            _cachedPublicKey = FallbackKey;
            return _cachedPublicKey;
        }

        private static string ConvertToHexString(byte[] bytes)
        {
            var sb = new StringBuilder(bytes.Length * 2);
            foreach (var b in bytes)
            {
                sb.Append(b.ToString("x2"));
            }
            return sb.ToString();
        }

        /// <summary>
        /// A stream wrapper that limits reading up to a specified end boundary.
        /// </summary>
        private class BoundedFileStream : Stream
        {
            private readonly FileStream _baseStream;
            private readonly long _length;
            private long _position;

            public BoundedFileStream(FileStream baseStream, long length)
            {
                _baseStream = baseStream ?? throw new ArgumentNullException(nameof(baseStream));
                _length = length;
                _position = 0;
                _baseStream.Position = 0;
            }

            public override bool CanRead => true;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length => _length;
            public override long Position { get => _position; set => throw new NotSupportedException(); }

            public override void Flush() { }

            public override int Read(byte[] buffer, int offset, int count)
            {
                if (_position >= _length) return 0;
                long remaining = _length - _position;
                int toRead = (int)Math.Min(count, remaining);
                _baseStream.Position = _position;
                int read = _baseStream.Read(buffer, offset, toRead);
                _position += read;
                return read;
            }

            public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            {
                if (_position >= _length) return 0;
                long remaining = _length - _position;
                int toRead = (int)Math.Min(count, remaining);
                _baseStream.Position = _position;
                int read = await _baseStream.ReadAsync(buffer, offset, toRead, cancellationToken).ConfigureAwait(false);
                _position += read;
                return read;
            }

            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        }
    }
}
