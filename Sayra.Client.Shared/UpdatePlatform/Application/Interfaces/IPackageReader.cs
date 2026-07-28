using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Shared.UpdatePlatform.Domain.Models;

namespace Sayra.Client.Shared.UpdatePlatform.Application.Interfaces
{
    /// <summary>
    /// Represents an enterprise package reader for stream-based parsing of SPK update packages.
    /// </summary>
    public interface IPackageReader : IDisposable
    {
        /// <summary>
        /// Gets a value indicating whether a package is currently open.
        /// </summary>
        bool IsOpen { get; }

        /// <summary>
        /// Opens a package from a local file path.
        /// </summary>
        Task OpenAsync(string packagePath, CancellationToken cancellationToken = default);

        /// <summary>
        /// Opens a package from a stream.
        /// </summary>
        Task OpenAsync(Stream packageStream, CancellationToken cancellationToken = default);

        /// <summary>
        /// Reads and parses the package header block.
        /// </summary>
        Task<PackageHeader> ReadHeaderAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Reads and parses the package manifest block.
        /// </summary>
        Task<UpdateManifest> ReadManifestAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Reads and parses the chunk metadata block.
        /// </summary>
        Task<IReadOnlyList<ChunkMetadata>> ReadChunksAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves a safe, read-only stream for a specific chunk index.
        /// </summary>
        Task<Stream> GetChunkStreamAsync(int chunkIndex, CancellationToken cancellationToken = default);

        /// <summary>
        /// Reads and parses the package footer block.
        /// </summary>
        Task<PackageFooter> ReadFooterAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Closes the currently open package.
        /// </summary>
        void Close();
    }
}
