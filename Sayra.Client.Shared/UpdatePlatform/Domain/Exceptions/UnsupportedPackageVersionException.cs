using System;

namespace Sayra.Client.Shared.UpdatePlatform.Domain.Exceptions
{
    /// <summary>
    /// Exception thrown when a package or manifest specifies a version, platform, or baseline that is not supported by the client.
    /// </summary>
    public class UnsupportedPackageVersionException : PackageException
    {
        public UnsupportedPackageVersionException() { }

        public UnsupportedPackageVersionException(string message) : base(message) { }

        public UnsupportedPackageVersionException(string message, Exception innerException) : base(message, innerException) { }
    }
}
