using System;

namespace Sayra.Client.Shared.Runtime.Launch.Domain.Exceptions
{
    public class LaunchException : Exception
    {
        public LaunchException(string message) : base(message) { }
        public LaunchException(string message, Exception innerException) : base(message, innerException) { }
    }
}
