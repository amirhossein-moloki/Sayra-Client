using System;
using Sayra.Client.Shared.UpdatePlatform.Domain.Exceptions;

namespace Sayra.Client.Shared.UpdatePlatform.Domain.Exceptions
{
    /// <summary>
    /// Thrown when a specific chunk download fails.
    /// </summary>
    public class ChunkDownloadException : UpdateException
    {
        public ChunkDownloadException() { }
        public ChunkDownloadException(string message) : base(message) { }
        public ChunkDownloadException(string message, Exception innerException) : base(message, innerException) { }
    }
}
