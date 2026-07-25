using System;

namespace Sayra.Client.Shared.Runtime.Launch.Domain.Exceptions
{
    public class UserSessionUnavailableException : LaunchException
    {
        public UserSessionUnavailableException(string message) : base(message) { }
        public UserSessionUnavailableException(string message, Exception innerException) : base(message, innerException) { }
    }
}
