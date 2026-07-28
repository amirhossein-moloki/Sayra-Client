using System;

namespace Sayra.Client.Shared.UpdatePlatform.Domain.Exceptions
{
    /// <summary>
    /// Exception thrown when package manifest parsing, structure, dependencies, or required fields fail validation checks.
    /// </summary>
    public class InvalidManifestException : PackageException
    {
        public InvalidManifestException() { }

        public InvalidManifestException(string message) : base(message) { }

        public InvalidManifestException(string message, Exception innerException) : base(message, innerException) { }
    }
}
