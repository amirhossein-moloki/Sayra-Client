using System;

namespace Sayra.Client.Authentication.Exceptions
{
    public class ProviderUnavailableException : AuthenticationException
    {
        public ProviderUnavailableException() : base() { }

        public ProviderUnavailableException(string message) : base(message) { }

        public ProviderUnavailableException(string message, Exception innerException) : base(message, innerException) { }
    }
}
