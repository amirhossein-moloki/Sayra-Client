using System;

namespace Sayra.Client.Shared.Runtime.Launch.Domain.Exceptions
{
    public class TokenCreationException : LaunchException
    {
        public TokenCreationException(string message) : base(message) { }
        public TokenCreationException(string message, Exception innerException) : base(message, innerException) { }
    }
}
