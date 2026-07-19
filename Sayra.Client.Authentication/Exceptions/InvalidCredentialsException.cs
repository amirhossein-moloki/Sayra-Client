using System;

namespace Sayra.Client.Authentication.Exceptions
{
    public class InvalidCredentialsException : AuthenticationException
    {
        public InvalidCredentialsException() : base() { }

        public InvalidCredentialsException(string message) : base(message) { }

        public InvalidCredentialsException(string message, Exception innerException) : base(message, innerException) { }
    }
}
