using System;

namespace Sayra.Client.Shared.UpdatePlatform.Domain.Exceptions
{
    /// <summary>
    /// Exception thrown when a cache entry or package file is corrupted, or fails integrity hash verification.
    /// </summary>
    public class CacheCorruptionException : StorageException
    {
        public CacheCorruptionException() { }

        public CacheCorruptionException(string message) : base(message) { }

        public CacheCorruptionException(string message, Exception innerException) : base(message, innerException) { }
    }
}
