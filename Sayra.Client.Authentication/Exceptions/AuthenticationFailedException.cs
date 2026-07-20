using System;

namespace Sayra.Client.Authentication.Exceptions
{
    public class AuthenticationFailedException : AuthenticationException
    {
        public AuthenticationFailedException() : base() { }

        public AuthenticationFailedException(string message) : base(message) { }

        public AuthenticationFailedException(string message, Exception innerException) : base(message, innerException) { }
    }
}
