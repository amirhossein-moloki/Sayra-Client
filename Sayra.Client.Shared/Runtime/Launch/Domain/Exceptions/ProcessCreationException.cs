using System;

namespace Sayra.Client.Shared.Runtime.Launch.Domain.Exceptions
{
    public class ProcessCreationException : LaunchException
    {
        public ProcessCreationException(string message) : base(message) { }
        public ProcessCreationException(string message, Exception innerException) : base(message, innerException) { }
    }
}
