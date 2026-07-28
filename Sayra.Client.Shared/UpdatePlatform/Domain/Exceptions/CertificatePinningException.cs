using System;

namespace Sayra.Client.Shared.UpdatePlatform.Domain.Exceptions
{
    /// <summary>
    /// Exception thrown when certificate pinning verification fails.
    /// </summary>
    public class CertificatePinningException : UpdateException
    {
        public CertificatePinningException() { }

        public CertificatePinningException(string message) : base(message) { }

        public CertificatePinningException(string message, Exception innerException) : base(message, innerException) { }
    }
}
