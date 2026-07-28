using System;

namespace Sayra.Client.Shared.UpdatePlatform.Domain.Exceptions
{
    /// <summary>
    /// Exception thrown when a downloaded update package or chunk is determined to be corrupt (e.g. SHA-256 hash mismatch).
    /// </summary>
    public class PackageCorruptedException : PackageException
    {
        public PackageCorruptedException() { }

        public PackageCorruptedException(string message) : base(message) { }

        public PackageCorruptedException(string message, Exception innerException) : base(message, innerException) { }
    }
}
