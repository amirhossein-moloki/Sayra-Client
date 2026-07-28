using System;

namespace Sayra.Client.Shared.UpdatePlatform.Domain.Exceptions
{
    /// <summary>
    /// Exception thrown when a package structure, layout, or header contains validation anomalies or is malformed.
    /// </summary>
    public class InvalidPackageException : PackageException
    {
        public InvalidPackageException() { }

        public InvalidPackageException(string message) : base(message) { }

        public InvalidPackageException(string message, Exception innerException) : base(message, innerException) { }
    }
}
