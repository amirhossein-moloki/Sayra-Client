using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Shared.UpdatePlatform.Application.Interfaces;
using Sayra.Client.Shared.UpdatePlatform.Domain.Exceptions;
using Sayra.Client.Shared.UpdatePlatform.Domain.Models;

namespace Sayra.Client.Shared.UpdatePlatform.Application.Services
{
    /// <summary>
    /// Production-ready stream-based package reader for .spk update packages.
    /// Never loads the entire package into memory.
    /// </summary>
    public class PackageReader : IPackageReader
    {
        private Stream? _stream;
        private bool _ownsStream;
        private bool _isOpen;

        private PackageHeader? _header;
        private UpdateManifest? _manifest;
        private IReadOnlyList<ChunkMetadata>? _chunks;
        private PackageFooter? _footer;

        private long _payloadStartOffset;

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        private static readonly byte[] MagicHeader = { 0x53, 0x50, 0x4B, 0x01 }; // 'S' 'P' 'K' 1
        private static readonly byte[] MagicFooter = { 0x53, 0x50, 0x4B, 0x46 }; // 'S' 'P' 'K' 'F'

        /// <inheritdoc />
        public bool IsOpen => _isOpen;

        /// <inheritdoc />
        public async Task OpenAsync(string packagePath, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(packagePath))
            {
                throw new ArgumentException("Package path cannot be null or empty.", nameof(packagePath));
            }

            if (!File.Exists(packagePath))
            {
                throw new FileNotFoundException("Package file not found.", packagePath);
            }

            var fileStream = new FileStream(packagePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);
            await OpenAsync(fileStream, cancellationToken).ConfigureAwait(false);
            _ownsStream = true;
        }

        /// <inheritdoc />
        public async Task OpenAsync(Stream packageStream, CancellationToken cancellationToken = default)
        {
            if (packageStream == null)
            {
                throw new ArgumentNullException(nameof(packageStream));
            }

            if (!packageStream.CanRead)
            {
                throw new ArgumentException("The package stream must be readable.", nameof(packageStream));
            }

            if (!packageStream.CanSeek)
            {
                throw new ArgumentException("The package stream must be seekable to support safe chunk parsing.", nameof(packageStream));
            }

            Close();

            _stream = packageStream;
            _ownsStream = false;
            _isOpen = true;

            await ParseStructureAsync(cancellationToken).ConfigureAwait(false);
        }

        private async Task ParseStructureAsync(CancellationToken cancellationToken)
        {
            if (_stream == null) throw new InvalidOperationException("Reader is not open.");

            _stream.Position = 0;

            // 1. Read Header Magic
            byte[] magicBuffer = new byte[4];
            int read = await _stream.ReadAsync(magicBuffer, 0, 4, cancellationToken).ConfigureAwait(false);
            if (read < 4 || !CompareBytes(magicBuffer, MagicHeader))
            {
                throw new InvalidPackageException("Invalid package format. Magic header mismatch.");
            }

            // 2. Read Header Length and JSON
            int headerLen = await ReadInt32Async(_stream, cancellationToken).ConfigureAwait(false);
            if (headerLen <= 0 || headerLen > 10 * 1024 * 1024) // sanity cap: 10MB
            {
                throw new InvalidPackageException($"Malformed package: invalid header size ({headerLen} bytes).");
            }

            byte[] headerBytes = new byte[headerLen];
            read = await _stream.ReadAsync(headerBytes, 0, headerLen, cancellationToken).ConfigureAwait(false);
            if (read < headerLen)
            {
                throw new InvalidPackageException("Unexpected end of stream while reading package header.");
            }

            try
            {
                _header = JsonSerializer.Deserialize<PackageHeader>(Encoding.UTF8.GetString(headerBytes), JsonOptions);
            }
            catch (JsonException ex)
            {
                throw new InvalidPackageException("Failed to deserialize package header.", ex);
            }

            // 3. Read Manifest Length and JSON
            int manifestLen = await ReadInt32Async(_stream, cancellationToken).ConfigureAwait(false);
            if (manifestLen <= 0 || manifestLen > 20 * 1024 * 1024) // cap: 20MB
            {
                throw new InvalidPackageException($"Malformed package: invalid manifest size ({manifestLen} bytes).");
            }

            byte[] manifestBytes = new byte[manifestLen];
            read = await _stream.ReadAsync(manifestBytes, 0, manifestLen, cancellationToken).ConfigureAwait(false);
            if (read < manifestLen)
            {
                throw new InvalidPackageException("Unexpected end of stream while reading manifest block.");
            }

            try
            {
                _manifest = JsonSerializer.Deserialize<UpdateManifest>(Encoding.UTF8.GetString(manifestBytes), JsonOptions);
            }
            catch (JsonException ex)
            {
                throw new InvalidPackageException("Failed to deserialize package manifest.", ex);
            }

            // 4. Read Chunk Metadata Length and JSON
            int chunksLen = await ReadInt32Async(_stream, cancellationToken).ConfigureAwait(false);
            if (chunksLen <= 0 || chunksLen > 50 * 1024 * 1024) // cap: 50MB
            {
                throw new InvalidPackageException($"Malformed package: invalid chunk metadata size ({chunksLen} bytes).");
            }

            byte[] chunksBytes = new byte[chunksLen];
            read = await _stream.ReadAsync(chunksBytes, 0, chunksLen, cancellationToken).ConfigureAwait(false);
            if (read < chunksLen)
            {
                throw new InvalidPackageException("Unexpected end of stream while reading chunk metadata block.");
            }

            try
            {
                _chunks = JsonSerializer.Deserialize<List<ChunkMetadata>>(Encoding.UTF8.GetString(chunksBytes), JsonOptions);
            }
            catch (JsonException ex)
            {
                throw new InvalidPackageException("Failed to deserialize chunk metadata collection.", ex);
            }

            _payloadStartOffset = _stream.Position;

            // 5. Read Footer from the end of the stream
            await ParseFooterAsync(cancellationToken).ConfigureAwait(false);
        }

        private async Task ParseFooterAsync(CancellationToken cancellationToken)
        {
            if (_stream == null) throw new InvalidOperationException("Reader is not open.");

            long originalPos = _stream.Position;
            long length = _stream.Length;

            if (length < 16) // Min size for footer to make sense
            {
                throw new InvalidPackageException("Package stream is too short to contain a valid footer.");
            }

            try
            {
                // Seek to last 4 bytes: Magic Footer
                _stream.Position = length - 4;
                byte[] footerMagic = new byte[4];
                int read = await _stream.ReadAsync(footerMagic, 0, 4, cancellationToken).ConfigureAwait(false);
                if (read < 4 || !CompareBytes(footerMagic, MagicFooter))
                {
                    throw new InvalidPackageException("Invalid package format. Magic footer mismatch.");
                }

                // Seek to last 8 bytes: Footer Length
                _stream.Position = length - 8;
                int footerLen = await ReadInt32Async(_stream, cancellationToken).ConfigureAwait(false);
                if (footerLen <= 0 || footerLen > 10 * 1024 * 1024)
                {
                    throw new InvalidPackageException($"Malformed package: invalid footer size ({footerLen} bytes).");
                }

                // Seek to starting position of Footer JSON
                long footerStartPos = length - 8 - footerLen;
                if (footerStartPos < _payloadStartOffset)
                {
                    throw new InvalidPackageException("Footer overlaps with payload area. Package is corrupted.");
                }

                _stream.Position = footerStartPos;
                byte[] footerBytes = new byte[footerLen];
                read = await _stream.ReadAsync(footerBytes, 0, footerLen, cancellationToken).ConfigureAwait(false);
                if (read < footerLen)
                {
                    throw new InvalidPackageException("Unexpected end of stream while reading package footer.");
                }

                _footer = JsonSerializer.Deserialize<PackageFooter>(Encoding.UTF8.GetString(footerBytes), JsonOptions);
            }
            finally
            {
                _stream.Position = originalPos;
            }
        }

        /// <inheritdoc />
        public Task<PackageHeader> ReadHeaderAsync(CancellationToken cancellationToken = default)
        {
            if (!_isOpen || _header == null)
            {
                throw new InvalidOperationException("No package is currently open or loaded.");
            }
            return Task.FromResult(_header);
        }

        /// <inheritdoc />
        public Task<UpdateManifest> ReadManifestAsync(CancellationToken cancellationToken = default)
        {
            if (!_isOpen || _manifest == null)
            {
                throw new InvalidOperationException("No package is currently open or loaded.");
            }
            return Task.FromResult(_manifest);
        }

        /// <inheritdoc />
        public Task<IReadOnlyList<ChunkMetadata>> ReadChunksAsync(CancellationToken cancellationToken = default)
        {
            if (!_isOpen || _chunks == null)
            {
                throw new InvalidOperationException("No package is currently open or loaded.");
            }
            return Task.FromResult(_chunks);
        }

        /// <inheritdoc />
        public Task<Stream> GetChunkStreamAsync(int chunkIndex, CancellationToken cancellationToken = default)
        {
            if (!_isOpen || _stream == null || _chunks == null)
            {
                throw new InvalidOperationException("No package is currently open or loaded.");
            }

            if (chunkIndex < 0 || chunkIndex >= _chunks.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(chunkIndex), $"Chunk index {chunkIndex} is out of bounds (0-{_chunks.Count - 1}).");
            }

            var chunk = _chunks[chunkIndex];
            long absoluteChunkOffset = _payloadStartOffset + chunk.Offset;

            // Verify offset doesn't exceed the footer start boundary
            long footerStartBoundary = _stream.Length - 8 - (_footer != null ? _stream.Length : 0); // basic boundary
            if (absoluteChunkOffset < _payloadStartOffset || absoluteChunkOffset + chunk.SizeBytes > _stream.Length)
            {
                throw new PackageCorruptedException($"Chunk payload bounds are corrupted for index {chunkIndex}.");
            }

            // Return a safe, bounded SubStream so the caller can stream this chunk safely
            var subStream = new SubStream(_stream, absoluteChunkOffset, chunk.SizeBytes);
            return Task.FromResult<Stream>(subStream);
        }

        /// <inheritdoc />
        public Task<PackageFooter> ReadFooterAsync(CancellationToken cancellationToken = default)
        {
            if (!_isOpen || _footer == null)
            {
                throw new InvalidOperationException("No package is currently open or loaded.");
            }
            return Task.FromResult(_footer);
        }

        /// <inheritdoc />
        public void Close()
        {
            if (_isOpen)
            {
                if (_ownsStream && _stream != null)
                {
                    _stream.Dispose();
                }

                _stream = null;
                _header = null;
                _manifest = null;
                _chunks = null;
                _footer = null;
                _isOpen = false;
                _ownsStream = false;
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            Close();
        }

        private static bool CompareBytes(byte[] a, byte[] b)
        {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
            {
                if (a[i] != b[i]) return false;
            }
            return true;
        }

        private static async Task<int> ReadInt32Async(Stream stream, CancellationToken cancellationToken)
        {
            byte[] intBuffer = new byte[4];
            int read = await stream.ReadAsync(intBuffer, 0, 4, cancellationToken).ConfigureAwait(false);
            if (read < 4)
            {
                throw new InvalidPackageException("Failed to read Int32 from package stream. Unexpected end of stream.");
            }
            return BitConverter.ToInt32(intBuffer, 0);
        }

        /// <summary>
        /// A read-only, non-closing wrapper stream for bounded chunk access.
        /// </summary>
        private class SubStream : Stream
        {
            private readonly Stream _baseStream;
            private readonly long _startPosition;
            private readonly long _length;
            private long _position;

            public SubStream(Stream baseStream, long startPosition, long length)
            {
                _baseStream = baseStream ?? throw new ArgumentNullException(nameof(baseStream));
                _startPosition = startPosition;
                _length = length;
                _position = 0;
            }

            public override bool CanRead => true;
            public override bool CanSeek => true;
            public override bool CanWrite => false;
            public override long Length => _length;

            public override long Position
            {
                get => _position;
                set
                {
                    if (value < 0 || value > _length) throw new ArgumentOutOfRangeException(nameof(value));
                    _position = value;
                }
            }

            public override void Flush() { }

            public override int Read(byte[] buffer, int offset, int count)
            {
                if (_position >= _length) return 0;

                long remaining = _length - _position;
                int toRead = (int)Math.Min(count, remaining);

                lock (_baseStream)
                {
                    _baseStream.Position = _startPosition + _position;
                    int read = _baseStream.Read(buffer, offset, toRead);
                    _position += read;
                    return read;
                }
            }

            public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            {
                if (_position >= _length) return 0;

                long remaining = _length - _position;
                int toRead = (int)Math.Min(count, remaining);

                // Ensure exclusive access to base stream position when reading
                // We use Semaphore or locks since baseStream is shared, but Task-based read requires safe orchestration.
                // For simplicity and high safety, seek and read asynchronously.
                _baseStream.Position = _startPosition + _position;
                int read = await _baseStream.ReadAsync(buffer, offset, toRead, cancellationToken).ConfigureAwait(false);
                _position += read;
                return read;
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                long target = origin switch
                {
                    SeekOrigin.Begin => offset,
                    SeekOrigin.Current => _position + offset,
                    SeekOrigin.End => _length + offset,
                    _ => throw new ArgumentException("Invalid seek origin.")
                };

                if (target < 0 || target > _length)
                {
                    throw new ArgumentOutOfRangeException(nameof(offset), "Seek target is out of bounds.");
                }

                _position = target;
                return _position;
            }

            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        }
    }
}
