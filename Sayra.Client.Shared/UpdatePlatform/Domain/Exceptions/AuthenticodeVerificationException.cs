using System;

namespace Sayra.Client.Shared.UpdatePlatform.Domain.Exceptions
{
    /// <summary>
    /// Exception thrown when Authenticode signature verification fails.
    /// </summary>
    public class AuthenticodeVerificationException : UpdateException
    {
        public AuthenticodeVerificationException() { }

        public AuthenticodeVerificationException(string message) : base(message) { }

        public AuthenticodeVerificationException(string message, Exception innerException) : base(message, innerException) { }
    }
}
