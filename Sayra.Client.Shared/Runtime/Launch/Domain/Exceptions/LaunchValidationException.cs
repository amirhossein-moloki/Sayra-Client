using System;

namespace Sayra.Client.Shared.Runtime.Launch.Domain.Exceptions
{
    public class LaunchValidationException : LaunchException
    {
        public LaunchValidationException(string message) : base(message) { }
        public LaunchValidationException(string message, Exception innerException) : base(message, innerException) { }
    }
}
