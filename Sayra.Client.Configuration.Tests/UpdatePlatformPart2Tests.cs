using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Sayra.Client.Shared.UpdatePlatform.Application.Interfaces;
using Sayra.Client.Shared.UpdatePlatform.Application.Services;
using Sayra.Client.Shared.UpdatePlatform.Application.Validation;
using Sayra.Client.Shared.UpdatePlatform.Domain.Enums;
using Sayra.Client.Shared.UpdatePlatform.Domain.Exceptions;
using Sayra.Client.Shared.UpdatePlatform.Domain.Models;

namespace Sayra.Client.Configuration.Tests
{
    /// <summary>
    /// Comprehensive test suite verifying SPK Package Format, Manifest Parsing,
    /// Chunk Metadata, Package Reading, Validation, and Cryptographic Verification.
    /// </summary>
    public class UpdatePlatformPart2Tests
    {
        private readonly IVersionValidator _versionValidator;
        private readonly IPackageValidator _packageValidator;
        private readonly IManifestParser _manifestParser;
        private readonly ISignatureVerifier _signatureVerifier;
        private readonly IPackageVerifier _packageVerifier;
        private readonly SpkPackageWriter _packageWriter;

        public UpdatePlatformPart2Tests()
        {
            _versionValidator = new VersionValidator();
            _packageValidator = new PackageValidator(_versionValidator);
            _manifestParser = new ManifestParser();
            _signatureVerifier = new SignatureVerifier();
            _packageVerifier = new PackageVerifier(_signatureVerifier, _packageValidator);
            _packageWriter = new SpkPackageWriter();
        }

        #region Helper: Generate Test Key Pair
        private (string PrivateKeyPem, string PublicKeyPem) GenerateECKeyPairs()
        {
            using (var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP384))
            {
                string privatePem = ecdsa.ExportPkcs8PrivateKeyPem();
                string publicPem = ecdsa.ExportSubjectPublicKeyInfoPem();
                return (privatePem, publicPem);
            }
        }

        private byte[] SignDataECDsa(byte[] data, string privateKeyPem)
        {
            using (var ecdsa = ECDsa.Create())
            {
                ecdsa.ImportFromPem(privateKeyPem);
                using (var sha256 = SHA256.Create())
                {
                    byte[] hash = sha256.ComputeHash(data);
                    return ecdsa.SignHash(hash);
                }
            }
        }
        #endregion

        #region SPK Format & Reader/Writer Tests

        [Fact]
        public async Task Test_SpkPackageWriteAndRead_ValidFlow()
        {
            // Arrange
            var manifest = new UpdateManifest
            {
                Id = Guid.NewGuid(),
                Version = "2.4.0",
                ProductName = "SAYRA Client",
                Description = "Major Update",
                PackageType = PackageType.FullPackage,
                UpdateType = UpdateType.Full,
                ReleaseDate = DateTime.UtcNow,
                Priority = UpdatePriority.Critical,
                Channel = UpdateChannel.Stable,
                SignatureMetadata = "dummy-sig"
            };

            var chunks = new List<ChunkMetadata>
            {
                new ChunkMetadata { Index = 0, Sha256Checksum = "e3b0c442" },
                new ChunkMetadata { Index = 1, Sha256Checksum = "a1b2c3d4" }
            };

            var chunkPayloads = new List<byte[]>
            {
                Encoding.UTF8.GetBytes("Payload chunk zero data content"),
                Encoding.UTF8.GetBytes("Payload chunk one data content")
            };

            using (var ms = new MemoryStream())
            {
                // Act - Write package
                await _packageWriter.WritePackageAsync(ms, manifest, chunks, chunkPayloads, "footer-sig", "fingerprint", CancellationToken.None);

                // Reset stream position for reading
                ms.Position = 0;

                // Act - Read package
                using (var reader = new PackageReader())
                {
                    await reader.OpenAsync(ms, CancellationToken.None);

                    var readHeader = await reader.ReadHeaderAsync();
                    var readManifest = await reader.ReadManifestAsync();
                    var readChunks = await reader.ReadChunksAsync();
                    var readFooter = await reader.ReadFooterAsync();

                    // Assert
                    Assert.Equal(manifest.Id, readHeader.PackageId);
                    Assert.Equal("2.4.0", readHeader.Version);
                    Assert.Equal(manifest.Id, readManifest.Id);
                    Assert.Equal(2, readChunks.Count);
                    Assert.Equal("footer-sig", readFooter.Signature);
                    Assert.Equal("fingerprint", readFooter.KeyFingerprint);

                    // Stream first chunk
                    using (var s0 = await reader.GetChunkStreamAsync(0))
                    using (var sr0 = new StreamReader(s0))
                    {
                        string content0 = await sr0.ReadToEndAsync();
                        Assert.Equal("Payload chunk zero data content", content0);
                    }

                    // Stream second chunk
                    using (var s1 = await reader.GetChunkStreamAsync(1))
                    using (var sr1 = new StreamReader(s1))
                    {
                        string content1 = await sr1.ReadToEndAsync();
                        Assert.Equal("Payload chunk one data content", content1);
                    }
                }
            }
        }

        #endregion

        #region Package Verification & Integrity Tests

        [Fact]
        public async Task Test_PackageVerifier_VerifiesValidSignedPackage()
        {
            // Arrange
            var keys = GenerateECKeyPairs();

            var manifest = new UpdateManifest
            {
                Id = Guid.NewGuid(),
                Version = "2.4.0",
                ProductName = "SAYRA Client",
                Description = "Major Update",
                PackageType = PackageType.FullPackage,
                UpdateType = UpdateType.Full,
                ReleaseDate = DateTime.UtcNow,
                Priority = UpdatePriority.Critical,
                Channel = UpdateChannel.Stable,
                SignatureMetadata = "dummy-sig"
            };

            var chunk0Data = Encoding.UTF8.GetBytes("Chunk data zero");
            var chunk1Data = Encoding.UTF8.GetBytes("Chunk data one");

            string chunk0Hash;
            string chunk1Hash;
            using (var sha256 = SHA256.Create())
            {
                chunk0Hash = BitConverter.ToString(sha256.ComputeHash(chunk0Data)).Replace("-", "").ToLower();
                chunk1Hash = BitConverter.ToString(sha256.ComputeHash(chunk1Data)).Replace("-", "").ToLower();
            }

            var chunks = new List<ChunkMetadata>
            {
                new ChunkMetadata { Index = 0, Sha256Checksum = chunk0Hash, Compression = "None", IsEncrypted = false },
                new ChunkMetadata { Index = 1, Sha256Checksum = chunk1Hash, Compression = "None", IsEncrypted = false }
            };

            var chunkPayloads = new List<byte[]> { chunk0Data, chunk1Data };

            // We must compute a real signature over the entire package preceding the footer block!
            // To do this, we can first compile the package without a signature (using dummy signature),
            // get the block preceding the footer, sign it, and rewrite the package with the real signature.
            using (var ms = new MemoryStream())
            {
                // Write package with temporary signature
                await _packageWriter.WritePackageAsync(ms, manifest, chunks, chunkPayloads, "temp-sig", "fingerprint", CancellationToken.None);

                // Retrieve preceding file block start position
                long totalLength = ms.Length;
                // Calculate starting position of the footer JSON
                // In WritePackageAsync, the footer is written as:
                // footerLenBytes (4 bytes) + footerBytes + MagicFooter (4 bytes)
                // Let's find footer bytes len
                ms.Position = totalLength - 8;
                byte[] footerLenBytes = new byte[4];
                ms.Read(footerLenBytes, 0, 4);
                int footerLen = BitConverter.ToInt32(footerLenBytes, 0);

                long precedingBlockLength = totalLength - 8 - footerLen;
                byte[] precedingBlock = new byte[precedingBlockLength];
                ms.Position = 0;
                ms.Read(precedingBlock, 0, (int)precedingBlockLength);

                // Sign the preceding block
                byte[] signatureBytes = SignDataECDsa(precedingBlock, keys.PrivateKeyPem);
                string realSignature = Convert.ToBase64String(signatureBytes);

                // Re-write the package with the actual signature
                using (var finalMs = new MemoryStream())
                {
                    await _packageWriter.WritePackageAsync(finalMs, manifest, chunks, chunkPayloads, realSignature, "fingerprint", CancellationToken.None);

                    // Write to file for verification
                    string tempFilePath = Path.GetTempFileName();
                    try
                    {
                        await File.WriteAllBytesAsync(tempFilePath, finalMs.ToArray());

                        // Configure verifier to use our generated public key
                        var mockVerifier = new PackageVerifier(_signatureVerifier, _packageValidator);
                        // Inject our public key to be used by the verifier (we can write it to server_public.key in directory)
                        string publicPemPath = Path.Combine(AppContext.BaseDirectory, "server_public.key");
                        await File.WriteAllTextAsync(publicPemPath, keys.PublicKeyPem);

                        // Act & Assert
                        var metadata = new UpdatePackage
                        {
                            PackageId = manifest.Id,
                            Version = manifest.Version,
                            Size = finalMs.Length,
                            Hash = realSignature
                        };

                        bool isIntegrityValid = await mockVerifier.VerifyPackageIntegrityAsync(tempFilePath, metadata, CancellationToken.None);
                        Assert.True(isIntegrityValid);
                    }
                    finally
                    {
                        if (File.Exists(tempFilePath)) File.Delete(tempFilePath);
                    }
                }
            }
        }

        [Fact]
        public async Task Test_PackageVerifier_RejectsTamperedChunk()
        {
            // Arrange
            var keys = GenerateECKeyPairs();

            var manifest = new UpdateManifest
            {
                Id = Guid.NewGuid(),
                Version = "2.4.0",
                ProductName = "SAYRA Client",
                Description = "Major Update",
                PackageType = PackageType.FullPackage,
                UpdateType = UpdateType.Full,
                ReleaseDate = DateTime.UtcNow,
                Priority = UpdatePriority.Critical,
                Channel = UpdateChannel.Stable,
                SignatureMetadata = "dummy-sig"
            };

            var chunk0Data = Encoding.UTF8.GetBytes("Chunk data zero");
            var chunk0Hash = "invalid-hash-value-to-cause-failure";

            var chunks = new List<ChunkMetadata>
            {
                new ChunkMetadata { Index = 0, Sha256Checksum = chunk0Hash, Compression = "None", IsEncrypted = false }
            };

            var chunkPayloads = new List<byte[]> { chunk0Data };

            using (var finalMs = new MemoryStream())
            {
                await _packageWriter.WritePackageAsync(finalMs, manifest, chunks, chunkPayloads, "dummy-sig", "fingerprint", CancellationToken.None);

                string tempFilePath = Path.GetTempFileName();
                try
                {
                    await File.WriteAllBytesAsync(tempFilePath, finalMs.ToArray());

                    var mockVerifier = new PackageVerifier(_signatureVerifier, _packageValidator);
                    string publicPemPath = Path.Combine(AppContext.BaseDirectory, "server_public.key");
                    await File.WriteAllTextAsync(publicPemPath, keys.PublicKeyPem);

                    var metadata = new UpdatePackage { PackageId = manifest.Id, Version = manifest.Version };

                    // Act & Assert - Should throw PackageCorruptedException due to Chunk Hash Mismatch
                    await Assert.ThrowsAsync<PackageCorruptedException>(() =>
                        mockVerifier.VerifyPackageIntegrityAsync(tempFilePath, metadata, CancellationToken.None));
                }
                finally
                {
                    if (File.Exists(tempFilePath)) File.Delete(tempFilePath);
                }
            }
        }

        [Fact]
        public async Task Test_PackageVerifier_RejectsTamperedEnvelopeSignature()
        {
            // Arrange
            var keys = GenerateECKeyPairs();

            var manifest = new UpdateManifest
            {
                Id = Guid.NewGuid(),
                Version = "2.4.0",
                ProductName = "SAYRA Client",
                Description = "Major Update",
                PackageType = PackageType.FullPackage,
                UpdateType = UpdateType.Full,
                ReleaseDate = DateTime.UtcNow,
                Priority = UpdatePriority.Critical,
                Channel = UpdateChannel.Stable,
                SignatureMetadata = "dummy-sig"
            };

            var chunk0Data = Encoding.UTF8.GetBytes("Chunk data zero");
            string chunk0Hash;
            using (var sha256 = SHA256.Create())
            {
                chunk0Hash = BitConverter.ToString(sha256.ComputeHash(chunk0Data)).Replace("-", "").ToLower();
            }

            var chunks = new List<ChunkMetadata>
            {
                new ChunkMetadata { Index = 0, Sha256Checksum = chunk0Hash }
            };

            var chunkPayloads = new List<byte[]> { chunk0Data };

            using (var finalMs = new MemoryStream())
            {
                // Write package with tampered/invalid envelope signature
                await _packageWriter.WritePackageAsync(finalMs, manifest, chunks, chunkPayloads, "invalid-tampered-envelope-signature", "fingerprint", CancellationToken.None);

                string tempFilePath = Path.GetTempFileName();
                try
                {
                    await File.WriteAllBytesAsync(tempFilePath, finalMs.ToArray());

                    var mockVerifier = new PackageVerifier(_signatureVerifier, _packageValidator);
                    string publicPemPath = Path.Combine(AppContext.BaseDirectory, "server_public.key");
                    await File.WriteAllTextAsync(publicPemPath, keys.PublicKeyPem);

                    var metadata = new UpdatePackage { PackageId = manifest.Id, Version = manifest.Version };

                    // Act & Assert - Should throw InvalidSignatureException due to signature mismatch
                    await Assert.ThrowsAsync<InvalidSignatureException>(() =>
                        mockVerifier.VerifyPackageIntegrityAsync(tempFilePath, metadata, CancellationToken.None));
                }
                finally
                {
                    if (File.Exists(tempFilePath)) File.Delete(tempFilePath);
                }
            }
        }

        #endregion

        #region Package Parser Negative Tests

        [Fact]
        public async Task Test_PackageReader_RejectsInvalidMagicHeader()
        {
            // Arrange
            byte[] invalidData = new byte[] { 0x00, 0x00, 0x00, 0x00, 0x01, 0x02, 0x03 };
            using (var ms = new MemoryStream(invalidData))
            using (var reader = new PackageReader())
            {
                // Act & Assert
                await Assert.ThrowsAsync<InvalidPackageException>(() => reader.OpenAsync(ms, CancellationToken.None));
            }
        }

        [Fact]
        public async Task Test_PackageReader_RejectsInvalidMagicFooter()
        {
            // Arrange
            var manifest = new UpdateManifest
            {
                Id = Guid.NewGuid(),
                Version = "2.4.0",
                ProductName = "SAYRA Client",
                ReleaseDate = DateTime.UtcNow
            };

            using (var ms = new MemoryStream())
            {
                await _packageWriter.WritePackageAsync(ms, manifest, new List<ChunkMetadata>(), new List<byte[]>(), "sig", "fp", CancellationToken.None);

                // Tamper with the footer magic bytes
                byte[] rawBytes = ms.ToArray();
                rawBytes[rawBytes.Length - 1] = 0x00; // corrupt footer magic

                using (var corruptedMs = new MemoryStream(rawBytes))
                using (var reader = new PackageReader())
                {
                    // Act & Assert
                    await Assert.ThrowsAsync<InvalidPackageException>(() => reader.OpenAsync(corruptedMs, CancellationToken.None));
                }
            }
        }

        #endregion

        #region Manifest Parser & Validator Tests

        [Fact]
        public void Test_ManifestParser_ParseAndSerialize_ValidCycle()
        {
            // Arrange
            var original = new UpdateManifest
            {
                Id = Guid.NewGuid(),
                Version = "1.0.5",
                ProductName = "SAYRA Kiosk",
                Description = "Fixes and features",
                PackageType = PackageType.FullPackage,
                UpdateType = UpdateType.Hotfix,
                ReleaseDate = DateTime.UtcNow,
                Channel = UpdateChannel.Stable
            };

            // Act
            string json = _manifestParser.Serialize(original);
            var parsed = _manifestParser.Parse(json);

            // Assert
            Assert.Equal(original.Id, parsed.Id);
            Assert.Equal(original.Version, parsed.Version);
            Assert.Equal(original.ProductName, parsed.ProductName);
            Assert.Equal(original.Description, parsed.Description);
            Assert.Equal(original.PackageType, parsed.PackageType);
            Assert.Equal(original.UpdateType, parsed.UpdateType);
            Assert.Equal(original.Channel, parsed.Channel);
        }

        [Fact]
        public void Test_ManifestParser_Parse_ThrowsOnMalformedJson()
        {
            // Act & Assert
            Assert.Throws<InvalidManifestException>(() => _manifestParser.Parse("not-a-valid-json-string"));
        }

        [Fact]
        public async Task Test_PackageValidator_ValidatesMalformedManifest()
        {
            // Arrange
            var invalidManifest = new UpdateManifest
            {
                Id = Guid.Empty, // Invalid empty Guid
                Version = "invalid-version",
                ProductName = ""
            };

            // Act & Assert
            await Assert.ThrowsAsync<InvalidManifestException>(() => _packageValidator.ValidateManifestAsync(invalidManifest, CancellationToken.None));
        }

        [Fact]
        public async Task Test_PackageValidator_ValidatesMalformedChunks()
        {
            // Arrange - Chunks list contains empty index
            var chunks = new List<ChunkMetadata>();

            // Act & Assert
            await Assert.ThrowsAsync<InvalidPackageException>(() => _packageValidator.ValidateChunksAsync(chunks, CancellationToken.None));

            // Arrange - Chunk with bad index sequence
            var badSequenceChunks = new List<ChunkMetadata>
            {
                new ChunkMetadata { Index = 1, SizeBytes = 100 },
                new ChunkMetadata { Index = 0, SizeBytes = 100 }
            };

            await Assert.ThrowsAsync<InvalidPackageException>(() => _packageValidator.ValidateChunksAsync(badSequenceChunks, CancellationToken.None));
        }

        #endregion
    }
}
