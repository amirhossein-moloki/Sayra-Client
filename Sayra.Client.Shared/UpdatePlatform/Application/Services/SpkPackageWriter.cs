using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Shared.UpdatePlatform.Domain.Enums;
using Sayra.Client.Shared.UpdatePlatform.Domain.Models;

namespace Sayra.Client.Shared.UpdatePlatform.Application.Services
{
    /// <summary>
    /// Helper/utility service for compiling and writing secure .spk update packages.
    /// Primarily used for pipeline building and robust mock package creation.
    /// </summary>
    public class SpkPackageWriter
    {
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            WriteIndented = false
        };

        private static readonly byte[] MagicHeader = { 0x53, 0x50, 0x4B, 0x01 }; // 'S' 'P' 'K' 1
        private static readonly byte[] MagicFooter = { 0x53, 0x50, 0x4B, 0x46 }; // 'S' 'P' 'K' 'F'

        /// <summary>
        /// Compiles and writes a secure update package to the output stream.
        /// </summary>
        public async Task WritePackageAsync(
            Stream outputStream,
            UpdateManifest manifest,
            IReadOnlyList<ChunkMetadata> chunks,
            IReadOnlyList<byte[]> chunkPayloads,
            string signature,
            string publicKeyFingerprint,
            CancellationToken cancellationToken = default)
        {
            if (outputStream == null) throw new ArgumentNullException(nameof(outputStream));
            if (manifest == null) throw new ArgumentNullException(nameof(manifest));
            if (chunks == null) throw new ArgumentNullException(nameof(chunks));
            if (chunkPayloads == null) throw new ArgumentNullException(nameof(chunkPayloads));
            if (chunks.Count != chunkPayloads.Count)
            {
                throw new ArgumentException("Chunks collection count must match chunk payloads count.");
            }

            // 1. Prepare Header
            var header = new PackageHeader
            {
                PackageId = manifest.Id,
                Version = manifest.Version,
                TargetArchitecture = SystemArchitecture.X64,
                TotalSizeBytes = 0, // Will calculate below
                CreatedAt = manifest.ReleaseDate != default ? manifest.ReleaseDate : DateTime.UnixEpoch
            };

            // Calculate chunk offsets and total size
            long totalSize = 0;
            for (int i = 0; i < chunks.Count; i++)
            {
                var chunk = chunks[i];
                var payload = chunkPayloads[i];
                chunk.Index = i;
                chunk.SizeBytes = payload.Length;
                chunk.Offset = totalSize;
                totalSize += payload.Length;
            }
            header.TotalSizeBytes = totalSize;

            // 2. Write Header Block
            await outputStream.WriteAsync(MagicHeader, 0, 4, cancellationToken).ConfigureAwait(false);

            string headerJson = JsonSerializer.Serialize(header, JsonOptions);
            byte[] headerBytes = Encoding.UTF8.GetBytes(headerJson);
            await WriteInt32Async(outputStream, headerBytes.Length, cancellationToken).ConfigureAwait(false);
            await outputStream.WriteAsync(headerBytes, 0, headerBytes.Length, cancellationToken).ConfigureAwait(false);

            // 3. Write Manifest Block
            string manifestJson = JsonSerializer.Serialize(manifest, JsonOptions);
            byte[] manifestBytes = Encoding.UTF8.GetBytes(manifestJson);
            await WriteInt32Async(outputStream, manifestBytes.Length, cancellationToken).ConfigureAwait(false);
            await outputStream.WriteAsync(manifestBytes, 0, manifestBytes.Length, cancellationToken).ConfigureAwait(false);

            // 4. Write Chunk Metadata Block
            string chunksJson = JsonSerializer.Serialize(chunks, JsonOptions);
            byte[] chunksBytes = Encoding.UTF8.GetBytes(chunksJson);
            await WriteInt32Async(outputStream, chunksBytes.Length, cancellationToken).ConfigureAwait(false);
            await outputStream.WriteAsync(chunksBytes, 0, chunksBytes.Length, cancellationToken).ConfigureAwait(false);

            // 5. Write Chunk Payloads
            for (int i = 0; i < chunkPayloads.Count; i++)
            {
                byte[] payload = chunkPayloads[i];
                await outputStream.WriteAsync(payload, 0, payload.Length, cancellationToken).ConfigureAwait(false);
            }

            // 6. Write Footer Block
            var footer = new PackageFooter
            {
                Signature = signature,
                Algorithm = "ECDsa-P384",
                SignedAt = manifest.ReleaseDate != default ? manifest.ReleaseDate : DateTime.UnixEpoch,
                KeyFingerprint = publicKeyFingerprint
            };

            string footerJson = JsonSerializer.Serialize(footer, JsonOptions);
            byte[] footerBytes = Encoding.UTF8.GetBytes(footerJson);

            await outputStream.WriteAsync(footerBytes, 0, footerBytes.Length, cancellationToken).ConfigureAwait(false);
            await WriteInt32Async(outputStream, footerBytes.Length, cancellationToken).ConfigureAwait(false);

            await outputStream.WriteAsync(MagicFooter, 0, 4, cancellationToken).ConfigureAwait(false);
        }

        private static async Task WriteInt32Async(Stream stream, int value, CancellationToken cancellationToken)
        {
            byte[] bytes = BitConverter.GetBytes(value);
            await stream.WriteAsync(bytes, 0, 4, cancellationToken).ConfigureAwait(false);
        }
    }
}
