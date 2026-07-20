using System;

namespace Sayra.Client.Authentication.Exceptions
{
    public class AuthorizationException : AuthenticationException
    {
        public AuthorizationException() : base() { }

        public AuthorizationException(string message) : base(message) { }

        public AuthorizationException(string message, Exception innerException) : base(message, innerException) { }
    }
}
