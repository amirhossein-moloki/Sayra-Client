using System;

namespace Sayra.Client.Shared.UpdatePlatform.Domain.Exceptions
{
    /// <summary>
    /// Exception thrown when cryptographic verification or signature validation fails for a package, manifest, or file.
    /// </summary>
    public class InvalidSignatureException : PackageException
    {
        public InvalidSignatureException() { }

        public InvalidSignatureException(string message) : base(message) { }

        public InvalidSignatureException(string message, Exception innerException) : base(message, innerException) { }
    }
}
