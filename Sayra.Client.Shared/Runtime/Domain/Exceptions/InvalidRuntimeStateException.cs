using System;

namespace Sayra.Client.Shared.Runtime.Domain.Exceptions
{
    public class InvalidRuntimeStateException : RuntimeException
    {
        public InvalidRuntimeStateException() { }
        public InvalidRuntimeStateException(string message) : base(message) { }
        public InvalidRuntimeStateException(string message, Exception innerException) : base(message, innerException) { }
    }
}
