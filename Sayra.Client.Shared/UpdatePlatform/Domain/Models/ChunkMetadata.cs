using System;

namespace Sayra.Client.Shared.UpdatePlatform.Domain.Models
{
    /// <summary>
    /// Represents the metadata for a single package chunk.
    /// </summary>
    public class ChunkMetadata
    {
        /// <summary>
        /// Gets or sets the zero-based index of the chunk.
        /// </summary>
        public int Index { get; set; }

        /// <summary>
        /// Gets or sets the size of the chunk in bytes.
        /// </summary>
        public long SizeBytes { get; set; }

        /// <summary>
        /// Gets or sets the SHA-256 checksum of the chunk bytes.
        /// </summary>
        public string Sha256Checksum { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the byte offset of the chunk payload within the package stream.
        /// </summary>
        public long Offset { get; set; }

        /// <summary>
        /// Gets or sets the compression algorithm used (e.g. "None", "GZip", "BZip2", "LZMA").
        /// </summary>
        public string Compression { get; set; } = "None";

        /// <summary>
        /// Gets or sets a value indicating whether this chunk payload is encrypted.
        /// </summary>
        public bool IsEncrypted { get; set; }
    }
}
